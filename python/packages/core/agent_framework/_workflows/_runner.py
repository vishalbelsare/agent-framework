# Copyright (c) Microsoft. All rights reserved.

import asyncio
import logging
from collections import defaultdict
from collections.abc import AsyncGenerator, Sequence
from typing import Any

from ._checkpoint import CheckpointStorage, WorkflowCheckpoint
from ._checkpoint_encoding import DATACLASS_MARKER, MODEL_MARKER, decode_checkpoint_value
from ._const import EXECUTOR_STATE_KEY
from ._edge import EdgeGroup
from ._edge_runner import EdgeRunner, create_edge_runner
from ._events import WorkflowEvent
from ._executor import Executor
from ._runner_context import (
    Message,
    RunnerContext,
)
from ._shared_state import SharedState

logger = logging.getLogger(__name__)


class Runner:
    """A class to run a workflow in Pregel supersteps."""

    def __init__(
        self,
        edge_groups: Sequence[EdgeGroup],
        executors: dict[str, Executor],
        shared_state: SharedState,
        ctx: RunnerContext,
        max_iterations: int = 100,
        workflow_id: str | None = None,
    ) -> None:
        """Initialize the runner with edges, shared state, and context.

        Args:
            edge_groups: The edge groups of the workflow.
            executors: Map of executor IDs to executor instances.
            shared_state: The shared state for the workflow.
            ctx: The runner context for the workflow.
            max_iterations: The maximum number of iterations to run.
            workflow_id: The workflow ID for checkpointing.
        """
        self._executors = executors
        self._edge_runners = [create_edge_runner(group, executors) for group in edge_groups]
        self._edge_runner_map = self._parse_edge_runners(self._edge_runners)
        self._ctx = ctx
        self._iteration = 0
        self._max_iterations = max_iterations
        self._shared_state = shared_state
        self._workflow_id = workflow_id
        self._running = False
        self._resumed_from_checkpoint = False  # Track whether we resumed
        self.graph_signature_hash: str | None = None

        # Set workflow ID in context if provided
        if workflow_id:
            self._ctx.set_workflow_id(workflow_id)

    @property
    def context(self) -> RunnerContext:
        """Get the workflow context."""
        return self._ctx

    def reset_iteration_count(self) -> None:
        """Reset the iteration count to zero."""
        self._iteration = 0

    async def run_until_convergence(self) -> AsyncGenerator[WorkflowEvent, None]:
        """Run the workflow until no more messages are sent."""
        if self._running:
            raise RuntimeError("Runner is already running.")

        self._running = True
        try:
            # Emit any events already produced prior to entering loop
            if await self._ctx.has_events():
                logger.info("Yielding pre-loop events")
                for event in await self._ctx.drain_events():
                    yield event

            # Create first checkpoint if there are messages from initial execution
            if await self._ctx.has_messages() and self._ctx.has_checkpointing():
                if not self._resumed_from_checkpoint:
                    logger.info("Creating checkpoint after initial execution")
                    await self._create_checkpoint_if_enabled("after_initial_execution")
                else:
                    logger.info("Skipping 'after_initial_execution' checkpoint because we resumed from a checkpoint")

            while self._iteration < self._max_iterations:
                logger.info(f"Starting superstep {self._iteration + 1}")

                # Run iteration concurrently with live event streaming: we poll
                # for new events while the iteration coroutine progresses.
                iteration_task = asyncio.create_task(self._run_iteration())
                while not iteration_task.done():
                    try:
                        # Wait briefly for any new event; timeout allows progress checks
                        event = await asyncio.wait_for(self._ctx.next_event(), timeout=0.05)
                        yield event
                    except asyncio.TimeoutError:
                        # Periodically continue to let iteration advance
                        continue

                # Propagate errors from iteration, but first surface any pending events
                try:
                    await iteration_task
                except Exception:
                    # Make sure failure-related events (like ExecutorFailedEvent) are surfaced
                    if await self._ctx.has_events():
                        for event in await self._ctx.drain_events():
                            yield event
                    raise
                self._iteration += 1

                # Drain any straggler events emitted at tail end
                if await self._ctx.has_events():
                    for event in await self._ctx.drain_events():
                        yield event

                logger.info(f"Completed superstep {self._iteration}")

                # Create checkpoint after each superstep iteration
                await self._create_checkpoint_if_enabled(f"superstep_{self._iteration}")

                if not await self._ctx.has_messages():
                    break

            if self._iteration >= self._max_iterations and await self._ctx.has_messages():
                raise RuntimeError(f"Runner did not converge after {self._max_iterations} iterations.")

            logger.info(f"Workflow completed after {self._iteration} supersteps")
            # TODO(@taochen): iteration is reset to zero, even in the event of a request info event.
            # Should iteration be preserved in the event of a request info event?
            self._iteration = 0
            self._resumed_from_checkpoint = False  # Reset resume flag for next run
        finally:
            self._running = False

    async def _run_iteration(self) -> None:
        async def _deliver_messages(source_executor_id: str, messages: list[Message]) -> None:
            """Outer loop to concurrently deliver messages from all sources to their targets."""

            async def _deliver_message_inner(edge_runner: EdgeRunner, message: Message) -> bool:
                """Inner loop to deliver a single message through an edge runner."""
                return await edge_runner.send_message(message, self._shared_state, self._ctx)

            def _normalize_message_payload(message: Message) -> None:
                data = message.data
                if not isinstance(data, dict):
                    return
                if MODEL_MARKER not in data and DATACLASS_MARKER not in data:
                    return
                try:
                    decoded = decode_checkpoint_value(data)
                except Exception as exc:  # pragma: no cover - defensive
                    logger.debug("Failed to decode checkpoint payload during delivery: %s", exc)
                    return
                message.data = decoded

            # Route all messages through normal workflow edges
            associated_edge_runners = self._edge_runner_map.get(source_executor_id, [])
            if not associated_edge_runners:
                logger.warning(f"No outgoing edges found for executor {source_executor_id}; dropping messages.")
                return

            for message in messages:
                _normalize_message_payload(message)
                # Deliver a message through all edge runners associated with the source executor concurrently.
                tasks = [_deliver_message_inner(edge_runner, message) for edge_runner in associated_edge_runners]
                await asyncio.gather(*tasks)

        messages = await self._ctx.drain_messages()
        tasks = [_deliver_messages(source_executor_id, messages) for source_executor_id, messages in messages.items()]
        await asyncio.gather(*tasks)

    async def _create_checkpoint_if_enabled(self, checkpoint_type: str) -> str | None:
        """Create a checkpoint if checkpointing is enabled and attach a label and metadata."""
        if not self._ctx.has_checkpointing():
            return None

        try:
            # Auto-snapshot executor states
            await self._auto_snapshot_executor_states()
            checkpoint_category = "initial" if checkpoint_type == "after_initial_execution" else "superstep"
            metadata = {
                "superstep": self._iteration,
                "checkpoint_type": checkpoint_category,
            }
            if self.graph_signature_hash:
                metadata["graph_signature"] = self.graph_signature_hash
            checkpoint_id = await self._ctx.create_checkpoint(
                self._shared_state,
                self._iteration,
                metadata=metadata,
            )
            logger.info(f"Created {checkpoint_type} checkpoint: {checkpoint_id}")
            return checkpoint_id
        except Exception as e:
            logger.warning(f"Failed to create {checkpoint_type} checkpoint: {e}")
            return None

    async def _auto_snapshot_executor_states(self) -> None:
        """Populate executor state by calling snapshot hooks on executors if available.

        TODO(@taochen#1614): this method is potentially problematic if executors also call
        set_executor_state on the context directly. We should clarify the intended usage
        pattern for executor state management.

        Convention:
          - If an executor defines an async or sync method `snapshot_state(self) -> dict`, use it.
          - Else if it has a plain attribute `state` that is a dict, use that.
        Only JSON-serializable dicts should be provided by executors.
        """
        for exec_id, executor in self._executors.items():
            state_dict: dict[str, Any] | None = None
            snapshot = getattr(executor, "snapshot_state", None)
            try:
                if callable(snapshot):
                    maybe = snapshot()
                    if asyncio.iscoroutine(maybe):  # type: ignore[arg-type]
                        maybe = await maybe  # type: ignore[assignment]
                    if isinstance(maybe, dict):
                        state_dict = maybe  # type: ignore[assignment]
                else:
                    state_attr = getattr(executor, "state", None)
                    if isinstance(state_attr, dict):
                        state_dict = state_attr  # type: ignore[assignment]
            except Exception as ex:  # pragma: no cover
                logger.debug(f"Executor {exec_id} snapshot_state failed: {ex}")

            if state_dict is not None:
                try:
                    await self._set_executor_state(exec_id, state_dict)
                except Exception as ex:  # pragma: no cover
                    logger.debug(f"Failed to persist state for executor {exec_id}: {ex}")

    async def restore_from_checkpoint(
        self,
        checkpoint_id: str,
        checkpoint_storage: CheckpointStorage | None = None,
    ) -> bool:
        """Restore workflow state from a checkpoint.

        Args:
            checkpoint_id: The ID of the checkpoint to restore from
            checkpoint_storage: Optional storage to load checkpoints from when the
                runner context itself is not configured with checkpointing.

        Returns:
            True if restoration was successful, False otherwise
        """
        try:
            # Load the checkpoint
            checkpoint: WorkflowCheckpoint | None
            if self._ctx.has_checkpointing():
                checkpoint = await self._ctx.load_checkpoint(checkpoint_id)
            elif checkpoint_storage is not None:
                checkpoint = await checkpoint_storage.load_checkpoint(checkpoint_id)
            else:
                logger.warning("Context does not support checkpointing and no external storage was provided")
                return False

            if not checkpoint:
                logger.error(f"Checkpoint {checkpoint_id} not found")
                return False

            # Validate the loaded checkpoint against the workflow
            graph_hash = getattr(self, "graph_signature_hash", None)
            checkpoint_hash = (checkpoint.metadata or {}).get("graph_signature")
            if graph_hash and checkpoint_hash and graph_hash != checkpoint_hash:
                raise ValueError(
                    "Workflow graph has changed since the checkpoint was created. "
                    "Please rebuild the original workflow before resuming."
                )
            if graph_hash and not checkpoint_hash:
                logger.warning(
                    "Checkpoint %s does not include graph signature metadata; skipping topology validation.",
                    checkpoint_id,
                )

            self._workflow_id = checkpoint.workflow_id
            # Restore shared state
            await self._shared_state.import_state(decode_checkpoint_value(checkpoint.shared_state))
            # Restore executor states using the restored shared state
            await self._restore_executor_states()
            # Apply the checkpoint to the context
            await self._ctx.apply_checkpoint(checkpoint)
            # Mark the runner as resumed
            self._mark_resumed(checkpoint.iteration_count)

            logger.info(f"Successfully restored workflow from checkpoint: {checkpoint_id}")
            return True
        except ValueError:
            raise
        except Exception as e:
            logger.error(f"Failed to restore from checkpoint {checkpoint_id}: {e}")
            return False

    async def _restore_executor_states(self) -> None:
        has_executor_states = await self._shared_state.has(EXECUTOR_STATE_KEY)
        if not has_executor_states:
            return

        executor_states = await self._shared_state.get(EXECUTOR_STATE_KEY)
        if not isinstance(executor_states, dict):
            raise ValueError("Executor states in shared state is not a dictionary. Unable to restore.")

        for executor_id, state in executor_states.items():
            if not isinstance(executor_id, str):
                raise ValueError("Executor ID in executor states is not a string. Unable to restore.")
            if not isinstance(state, dict):
                raise ValueError(f"Executor state for {executor_id} is not a dictionary. Unable to restore.")

            executor = self._executors.get(executor_id)
            if not executor:
                raise ValueError(f"Executor {executor_id} not found during state restoration.")

            restored = False
            restore_method = getattr(executor, "restore_state", None)
            try:
                if callable(restore_method):
                    maybe = restore_method(state)
                    if asyncio.iscoroutine(maybe):  # type: ignore[arg-type]
                        await maybe  # type: ignore[arg-type]
                    restored = True
            except Exception as ex:  # pragma: no cover - defensive
                raise ValueError(f"Executor {executor_id} restore_state failed: {ex}") from ex

            if not restored:
                logger.debug(f"Executor {executor_id} does not support state restoration; skipping.")

    def _parse_edge_runners(self, edge_runners: list[EdgeRunner]) -> dict[str, list[EdgeRunner]]:
        """Parse the edge runners of the workflow into a mapping where each source executor ID maps to its edge runners.

        Args:
            edge_runners: A list of edge runners in the workflow.

        Returns:
            A dictionary mapping each source executor ID to a list of edge runners.
        """
        parsed: defaultdict[str, list[EdgeRunner]] = defaultdict(list)
        for runner in edge_runners:
            # Accessing protected attribute (_edge_group) intentionally for internal wiring.
            for source_executor_id in runner._edge_group.source_executor_ids:  # type: ignore[attr-defined]
                parsed[source_executor_id].append(runner)

        return parsed

    def _mark_resumed(self, iteration: int) -> None:
        """Mark the runner as having resumed from a checkpoint.

        Optionally set the current iteration and max iterations.
        """
        self._resumed_from_checkpoint = True
        self._iteration = iteration

    async def _set_executor_state(self, executor_id: str, state: dict[str, Any]) -> None:
        """Store executor state in shared state under a reserved key.

        Executors call this with a JSON-serializable dict capturing the minimal
        state needed to resume. It replaces any previously stored state.
        """
        has_existing_states = await self._shared_state.has(EXECUTOR_STATE_KEY)
        if has_existing_states:
            existing_states = await self._shared_state.get(EXECUTOR_STATE_KEY)
        else:
            existing_states = {}

        if not isinstance(existing_states, dict):
            raise ValueError("Existing executor states in shared state is not a dictionary.")

        existing_states[executor_id] = state
        await self._shared_state.set(EXECUTOR_STATE_KEY, existing_states)
