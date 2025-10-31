# Copyright (c) Microsoft. All rights reserved.

"""Agent Framework message mapper implementation."""

import json
import logging
import time
import uuid
from collections import OrderedDict
from collections.abc import Sequence
from datetime import datetime
from typing import Any, Union
from uuid import uuid4

from openai.types.responses import (
    Response,
    ResponseContentPartAddedEvent,
    ResponseCreatedEvent,
    ResponseError,
    ResponseFailedEvent,
    ResponseInProgressEvent,
)

from .models import (
    AgentFrameworkRequest,
    CustomResponseOutputItemAddedEvent,
    CustomResponseOutputItemDoneEvent,
    ExecutorActionItem,
    InputTokensDetails,
    OpenAIResponse,
    OutputTokensDetails,
    ResponseCompletedEvent,
    ResponseErrorEvent,
    ResponseFunctionCallArgumentsDeltaEvent,
    ResponseFunctionResultComplete,
    ResponseFunctionToolCall,
    ResponseOutputItemAddedEvent,
    ResponseOutputMessage,
    ResponseOutputText,
    ResponseReasoningTextDeltaEvent,
    ResponseStreamEvent,
    ResponseTextDeltaEvent,
    ResponseTraceEventComplete,
    ResponseUsage,
    ResponseWorkflowEventComplete,
)

logger = logging.getLogger(__name__)

# Type alias for all possible event types
EventType = Union[
    ResponseStreamEvent,
    ResponseWorkflowEventComplete,
    ResponseOutputItemAddedEvent,
    ResponseTraceEventComplete,
]


def _serialize_content_recursive(value: Any) -> Any:
    """Recursively serialize Agent Framework Content objects to JSON-compatible values.

    This handles nested Content objects (like TextContent inside FunctionResultContent.result)
    that can't be directly serialized by json.dumps().

    Args:
        value: Value to serialize (can be Content object, dict, list, primitive, etc.)

    Returns:
        JSON-serializable version with all Content objects converted to dicts/primitives
    """
    # Handle None and basic JSON-serializable types
    if value is None or isinstance(value, (str, int, float, bool)):
        return value

    # Check if it's a SerializationMixin (includes all Content types)
    # Content objects have to_dict() method
    if hasattr(value, "to_dict") and callable(getattr(value, "to_dict", None)):
        try:
            return value.to_dict()
        except Exception as e:
            # If to_dict() fails, fall through to other methods
            logger.debug(f"Failed to serialize with to_dict(): {e}")

    # Handle dictionaries - recursively process values
    if isinstance(value, dict):
        return {key: _serialize_content_recursive(val) for key, val in value.items()}

    # Handle lists and tuples - recursively process elements
    if isinstance(value, (list, tuple)):
        serialized = [_serialize_content_recursive(item) for item in value]
        # For single-item lists containing text Content, extract just the text
        # This handles the MCP case where result = [TextContent(text="Hello")]
        # and we want output = "Hello" not output = '[{"type": "text", "text": "Hello"}]'
        if len(serialized) == 1 and isinstance(serialized[0], dict) and serialized[0].get("type") == "text":
            return serialized[0].get("text", "")
        return serialized

    # For other objects with model_dump(), try that
    if hasattr(value, "model_dump") and callable(getattr(value, "model_dump", None)):
        try:
            return value.model_dump()
        except Exception as e:
            logger.debug(f"Failed to serialize with model_dump(): {e}")

    # Return as-is and let json.dumps handle it (may raise TypeError for non-serializable types)
    return value


class MessageMapper:
    """Maps Agent Framework messages/responses to OpenAI format."""

    def __init__(self, max_contexts: int = 1000) -> None:
        """Initialize Agent Framework message mapper.

        Args:
            max_contexts: Maximum number of contexts to keep in memory (default: 1000)
        """
        self.sequence_counter = 0
        self._conversion_contexts: OrderedDict[int, dict[str, Any]] = OrderedDict()
        self._max_contexts = max_contexts

        # Track usage per request for final Response.usage (OpenAI standard)
        self._usage_accumulator: dict[str, dict[str, int]] = {}

        # Register content type mappers for all 12 Agent Framework content types
        self.content_mappers = {
            "TextContent": self._map_text_content,
            "TextReasoningContent": self._map_reasoning_content,
            "FunctionCallContent": self._map_function_call_content,
            "FunctionResultContent": self._map_function_result_content,
            "ErrorContent": self._map_error_content,
            "UsageContent": self._map_usage_content,
            "DataContent": self._map_data_content,
            "UriContent": self._map_uri_content,
            "HostedFileContent": self._map_hosted_file_content,
            "HostedVectorStoreContent": self._map_hosted_vector_store_content,
            "FunctionApprovalRequestContent": self._map_approval_request_content,
            "FunctionApprovalResponseContent": self._map_approval_response_content,
        }

    async def convert_event(self, raw_event: Any, request: AgentFrameworkRequest) -> Sequence[Any]:
        """Convert a single Agent Framework event to OpenAI events.

        Args:
            raw_event: Agent Framework event (AgentRunResponseUpdate, WorkflowEvent, etc.)
            request: Original request for context

        Returns:
            List of OpenAI response stream events
        """
        context = self._get_or_create_context(request)

        # Handle error events
        if isinstance(raw_event, dict) and raw_event.get("type") == "error":
            return [await self._create_error_event(raw_event.get("message", "Unknown error"), context)]

        # Handle ResponseTraceEvent objects from our trace collector
        from .models import ResponseTraceEvent

        if isinstance(raw_event, ResponseTraceEvent):
            return [
                ResponseTraceEventComplete(
                    type="response.trace.complete",
                    data=raw_event.data,
                    item_id=context["item_id"],
                    sequence_number=self._next_sequence(context),
                )
            ]

        # Handle Agent lifecycle events first
        from .models._openai_custom import AgentCompletedEvent, AgentFailedEvent, AgentStartedEvent

        if isinstance(raw_event, (AgentStartedEvent, AgentCompletedEvent, AgentFailedEvent)):
            return await self._convert_agent_lifecycle_event(raw_event, context)

        # Import Agent Framework types for proper isinstance checks
        try:
            from agent_framework import AgentRunResponse, AgentRunResponseUpdate, WorkflowEvent
            from agent_framework._workflows._events import AgentRunUpdateEvent

            # Handle AgentRunUpdateEvent - workflow event wrapping AgentRunResponseUpdate
            # This must be checked BEFORE generic WorkflowEvent check
            if isinstance(raw_event, AgentRunUpdateEvent):
                # Extract the AgentRunResponseUpdate from the event's data attribute
                if raw_event.data and isinstance(raw_event.data, AgentRunResponseUpdate):
                    return await self._convert_agent_update(raw_event.data, context)
                # If no data, treat as generic workflow event
                return await self._convert_workflow_event(raw_event, context)

            # Handle complete agent response (AgentRunResponse) - for non-streaming agent execution
            if isinstance(raw_event, AgentRunResponse):
                return await self._convert_agent_response(raw_event, context)

            # Handle agent updates (AgentRunResponseUpdate) - for direct agent execution
            if isinstance(raw_event, AgentRunResponseUpdate):
                return await self._convert_agent_update(raw_event, context)

            # Handle workflow events (any class that inherits from WorkflowEvent)
            if isinstance(raw_event, WorkflowEvent):
                return await self._convert_workflow_event(raw_event, context)

        except ImportError as e:
            logger.warning(f"Could not import Agent Framework types: {e}")
            # Fallback to attribute-based detection
            if hasattr(raw_event, "contents"):
                return await self._convert_agent_update(raw_event, context)
            if hasattr(raw_event, "__class__") and "Event" in raw_event.__class__.__name__:
                return await self._convert_workflow_event(raw_event, context)

        # Unknown event type
        return [await self._create_unknown_event(raw_event, context)]

    async def aggregate_to_response(self, events: Sequence[Any], request: AgentFrameworkRequest) -> OpenAIResponse:
        """Aggregate streaming events into final OpenAI response.

        Args:
            events: List of OpenAI stream events
            request: Original request for context

        Returns:
            Final aggregated OpenAI response
        """
        try:
            # Extract text content from events
            content_parts = []

            for event in events:
                # Extract delta text from ResponseTextDeltaEvent
                if hasattr(event, "delta") and hasattr(event, "type") and event.type == "response.output_text.delta":
                    content_parts.append(event.delta)

            # Combine content
            full_content = "".join(content_parts)

            # Create proper OpenAI Response
            response_output_text = ResponseOutputText(type="output_text", text=full_content, annotations=[])

            response_output_message = ResponseOutputMessage(
                type="message",
                role="assistant",
                content=[response_output_text],
                id=f"msg_{uuid.uuid4().hex[:8]}",
                status="completed",
            )

            # Get usage from accumulator (OpenAI standard)
            request_id = str(id(request))
            usage_data = self._usage_accumulator.get(request_id)

            if usage_data:
                usage = ResponseUsage(
                    input_tokens=usage_data["input_tokens"],
                    output_tokens=usage_data["output_tokens"],
                    total_tokens=usage_data["total_tokens"],
                    input_tokens_details=InputTokensDetails(cached_tokens=0),
                    output_tokens_details=OutputTokensDetails(reasoning_tokens=0),
                )
                # Cleanup accumulator
                del self._usage_accumulator[request_id]
            else:
                # Fallback: estimate if no usage was tracked
                input_token_count = len(str(request.input)) // 4 if request.input else 0
                output_token_count = len(full_content) // 4
                usage = ResponseUsage(
                    input_tokens=input_token_count,
                    output_tokens=output_token_count,
                    total_tokens=input_token_count + output_token_count,
                    input_tokens_details=InputTokensDetails(cached_tokens=0),
                    output_tokens_details=OutputTokensDetails(reasoning_tokens=0),
                )

            return OpenAIResponse(
                id=f"resp_{uuid.uuid4().hex[:12]}",
                object="response",
                created_at=datetime.now().timestamp(),
                model=request.model,
                output=[response_output_message],
                usage=usage,
                parallel_tool_calls=False,
                tool_choice="none",
                tools=[],
            )

        except Exception as e:
            logger.exception(f"Error aggregating response: {e}")
            return await self._create_error_response(str(e), request)
        finally:
            # Cleanup: Remove context after aggregation to prevent memory leak
            # This handles the common case where streaming completes successfully
            request_key = id(request)
            if self._conversion_contexts.pop(request_key, None):
                logger.debug(f"Cleaned up context for request {request_key} after aggregation")

    def _get_or_create_context(self, request: AgentFrameworkRequest) -> dict[str, Any]:
        """Get or create conversion context for this request.

        Uses LRU eviction when max_contexts is reached to prevent unbounded memory growth.

        Args:
            request: Request to get context for

        Returns:
            Conversion context dictionary
        """
        request_key = id(request)

        if request_key not in self._conversion_contexts:
            # Evict oldest context if at capacity (LRU eviction)
            if len(self._conversion_contexts) >= self._max_contexts:
                evicted_key, _ = self._conversion_contexts.popitem(last=False)
                logger.debug(f"Evicted oldest context (key={evicted_key}) - at max capacity ({self._max_contexts})")

            self._conversion_contexts[request_key] = {
                "sequence_counter": 0,
                "item_id": f"msg_{uuid.uuid4().hex[:8]}",
                "content_index": 0,
                "output_index": 0,
                "request_id": str(request_key),  # For usage accumulation
                "request": request,  # Store the request for model name access
                # Track active function calls: {call_id: {name, item_id, args_chunks}}
                "active_function_calls": {},
            }
        else:
            # Move to end (mark as recently used for LRU)
            self._conversion_contexts.move_to_end(request_key)

        return self._conversion_contexts[request_key]

    def _next_sequence(self, context: dict[str, Any]) -> int:
        """Get next sequence number for events.

        Args:
            context: Conversion context

        Returns:
            Next sequence number
        """
        context["sequence_counter"] += 1
        return int(context["sequence_counter"])

    async def _convert_agent_update(self, update: Any, context: dict[str, Any]) -> Sequence[Any]:
        """Convert agent text updates to proper content part events.

        Args:
            update: Agent run response update
            context: Conversion context

        Returns:
            List of OpenAI response stream events
        """
        events: list[Any] = []

        try:
            # Handle different update types
            if not hasattr(update, "contents") or not update.contents:
                return events

            # Check if we're streaming text content
            has_text_content = any(content.__class__.__name__ == "TextContent" for content in update.contents)

            # If we have text content and haven't created a message yet, create one
            if has_text_content and "current_message_id" not in context:
                message_id = f"msg_{uuid4().hex[:8]}"
                context["current_message_id"] = message_id
                context["output_index"] = context.get("output_index", -1) + 1

                # Add message output item
                events.append(
                    ResponseOutputItemAddedEvent(
                        type="response.output_item.added",
                        output_index=context["output_index"],
                        sequence_number=self._next_sequence(context),
                        item=ResponseOutputMessage(
                            type="message", id=message_id, role="assistant", content=[], status="in_progress"
                        ),
                    )
                )

                # Add content part for text
                context["content_index"] = 0
                events.append(
                    ResponseContentPartAddedEvent(
                        type="response.content_part.added",
                        output_index=context["output_index"],
                        content_index=context["content_index"],
                        item_id=message_id,
                        sequence_number=self._next_sequence(context),
                        part=ResponseOutputText(type="output_text", text="", annotations=[]),
                    )
                )

            # Process each content item
            for content in update.contents:
                content_type = content.__class__.__name__

                # Special handling for TextContent to use proper delta events
                if content_type == "TextContent" and "current_message_id" in context:
                    # Stream text content via proper delta events
                    events.append(
                        ResponseTextDeltaEvent(
                            type="response.output_text.delta",
                            output_index=context["output_index"],
                            content_index=context.get("content_index", 0),
                            item_id=context["current_message_id"],
                            delta=content.text,
                            logprobs=[],  # We don't have logprobs from Agent Framework
                            sequence_number=self._next_sequence(context),
                        )
                    )
                elif content_type in self.content_mappers:
                    # Use existing mappers for other content types
                    mapped_events = await self.content_mappers[content_type](content, context)
                    if mapped_events is not None:  # Handle None returns (e.g., UsageContent)
                        if isinstance(mapped_events, list):
                            events.extend(mapped_events)
                        else:
                            events.append(mapped_events)
                else:
                    # Graceful fallback for unknown content types
                    events.append(await self._create_unknown_content_event(content, context))

                # Don't increment content_index for text deltas within the same part
                if content_type != "TextContent":
                    context["content_index"] = context.get("content_index", 0) + 1

        except Exception as e:
            logger.warning(f"Error converting agent update: {e}")
            events.append(await self._create_error_event(str(e), context))

        return events

    async def _convert_agent_response(self, response: Any, context: dict[str, Any]) -> Sequence[Any]:
        """Convert complete AgentRunResponse to OpenAI events.

        This handles non-streaming agent execution where agent.run() returns
        a complete AgentRunResponse instead of streaming AgentRunResponseUpdate objects.

        Args:
            response: Agent run response (AgentRunResponse)
            context: Conversion context

        Returns:
            List of OpenAI response stream events
        """
        events: list[Any] = []

        try:
            # Extract all messages from the response
            messages = getattr(response, "messages", [])

            # Convert each message's contents to streaming events
            for message in messages:
                if hasattr(message, "contents") and message.contents:
                    for content in message.contents:
                        content_type = content.__class__.__name__

                        if content_type in self.content_mappers:
                            mapped_events = await self.content_mappers[content_type](content, context)
                            if mapped_events is not None:  # Handle None returns (e.g., UsageContent)
                                if isinstance(mapped_events, list):
                                    events.extend(mapped_events)
                                else:
                                    events.append(mapped_events)
                        else:
                            # Graceful fallback for unknown content types
                            events.append(await self._create_unknown_content_event(content, context))

                        context["content_index"] += 1

            # Add usage information if present
            usage_details = getattr(response, "usage_details", None)
            if usage_details:
                from agent_framework import UsageContent

                usage_content = UsageContent(details=usage_details)
                await self._map_usage_content(usage_content, context)
                # Note: _map_usage_content returns None - it accumulates usage for final Response.usage

        except Exception as e:
            logger.warning(f"Error converting agent response: {e}")
            events.append(await self._create_error_event(str(e), context))

        return events

    async def _convert_agent_lifecycle_event(self, event: Any, context: dict[str, Any]) -> Sequence[Any]:
        """Convert agent lifecycle events to OpenAI response events.

        Args:
            event: AgentStartedEvent, AgentCompletedEvent, or AgentFailedEvent
            context: Conversion context

        Returns:
            List of OpenAI response stream events
        """
        from .models._openai_custom import AgentCompletedEvent, AgentFailedEvent, AgentStartedEvent

        try:
            # Get model name from context (the agent name)
            model_name = context.get("request", {}).model if context.get("request") else "agent"

            if isinstance(event, AgentStartedEvent):
                execution_id = f"agent_{uuid4().hex[:12]}"
                context["execution_id"] = execution_id

                # Create Response object
                response_obj = Response(
                    id=f"resp_{execution_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,
                    output=[],
                    status="in_progress",
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                # Emit both created and in_progress events
                return [
                    ResponseCreatedEvent(
                        type="response.created", sequence_number=self._next_sequence(context), response=response_obj
                    ),
                    ResponseInProgressEvent(
                        type="response.in_progress", sequence_number=self._next_sequence(context), response=response_obj
                    ),
                ]

            if isinstance(event, AgentCompletedEvent):
                execution_id = context.get("execution_id", f"agent_{uuid4().hex[:12]}")

                response_obj = Response(
                    id=f"resp_{execution_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,
                    output=[],
                    status="completed",
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                return [
                    ResponseCompletedEvent(
                        type="response.completed", sequence_number=self._next_sequence(context), response=response_obj
                    )
                ]

            if isinstance(event, AgentFailedEvent):
                execution_id = context.get("execution_id", f"agent_{uuid4().hex[:12]}")

                # Create error object
                response_error = ResponseError(
                    message=str(event.error) if event.error else "Unknown error", code="server_error"
                )

                response_obj = Response(
                    id=f"resp_{execution_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,
                    output=[],
                    status="failed",
                    error=response_error,
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                return [
                    ResponseFailedEvent(
                        type="response.failed", sequence_number=self._next_sequence(context), response=response_obj
                    )
                ]

            return []

        except Exception as e:
            logger.warning(f"Error converting agent lifecycle event: {e}")
            return [await self._create_error_event(str(e), context)]

    async def _convert_workflow_event(self, event: Any, context: dict[str, Any]) -> Sequence[Any]:
        """Convert workflow events to standard OpenAI event objects.

        Args:
            event: Workflow event
            context: Conversion context

        Returns:
            List of OpenAI response stream events
        """
        try:
            event_class = event.__class__.__name__

            # Response-level events - construct proper OpenAI objects
            if event_class == "WorkflowStartedEvent":
                workflow_id = getattr(event, "workflow_id", str(uuid4()))
                context["workflow_id"] = workflow_id

                # Import Response type for proper construction
                from openai.types.responses import Response

                # Return proper OpenAI event objects
                events: list[Any] = []

                # Determine the model name - use request model or default to "workflow"
                # The request model will be the agent name for agents, workflow name for workflows
                model_name = context.get("request", {}).model if context.get("request") else "workflow"

                # Create a full Response object with all required fields
                response_obj = Response(
                    id=f"resp_{workflow_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,  # Use the actual model/agent name
                    output=[],  # Empty output list initially
                    status="in_progress",
                    # Required fields with safe defaults
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                # First emit response.created
                events.append(
                    ResponseCreatedEvent(
                        type="response.created", sequence_number=self._next_sequence(context), response=response_obj
                    )
                )

                # Then emit response.in_progress (reuse same response object)
                events.append(
                    ResponseInProgressEvent(
                        type="response.in_progress", sequence_number=self._next_sequence(context), response=response_obj
                    )
                )

                return events

            if event_class in ["WorkflowCompletedEvent", "WorkflowOutputEvent"]:
                workflow_id = context.get("workflow_id", str(uuid4()))

                # Import Response type for proper construction
                from openai.types.responses import Response

                # Get model name from context
                model_name = context.get("request", {}).model if context.get("request") else "workflow"

                # Create a full Response object for completed state
                response_obj = Response(
                    id=f"resp_{workflow_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,
                    output=[],  # Output should be populated by this point from text streaming
                    status="completed",
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                return [
                    ResponseCompletedEvent(
                        type="response.completed", sequence_number=self._next_sequence(context), response=response_obj
                    )
                ]

            if event_class == "WorkflowFailedEvent":
                workflow_id = context.get("workflow_id", str(uuid4()))
                error_info = getattr(event, "error", None)

                # Import Response and ResponseError types
                from openai.types.responses import Response, ResponseError

                # Get model name from context
                model_name = context.get("request", {}).model if context.get("request") else "workflow"

                # Create error object
                error_message = str(error_info) if error_info else "Unknown error"

                # Create ResponseError object (code must be one of the allowed values)
                response_error = ResponseError(
                    message=error_message,
                    code="server_error",  # Use generic server_error code for workflow failures
                )

                # Create a full Response object for failed state
                response_obj = Response(
                    id=f"resp_{workflow_id}",
                    object="response",
                    created_at=float(time.time()),
                    model=model_name,
                    output=[],
                    status="failed",
                    error=response_error,
                    parallel_tool_calls=False,
                    tool_choice="none",
                    tools=[],
                )

                return [
                    ResponseFailedEvent(
                        type="response.failed", sequence_number=self._next_sequence(context), response=response_obj
                    )
                ]

            # Executor-level events (output items)
            if event_class == "ExecutorInvokedEvent":
                executor_id = getattr(event, "executor_id", "unknown")
                item_id = f"exec_{executor_id}_{uuid4().hex[:8]}"
                context[f"exec_item_{executor_id}"] = item_id
                context["output_index"] = context.get("output_index", -1) + 1

                # Create ExecutorActionItem with proper type
                executor_item = ExecutorActionItem(
                    type="executor_action",
                    id=item_id,
                    executor_id=executor_id,
                    status="in_progress",
                    metadata=getattr(event, "metadata", {}),
                )

                # Use our custom event type that accepts ExecutorActionItem
                return [
                    CustomResponseOutputItemAddedEvent(
                        type="response.output_item.added",
                        output_index=context["output_index"],
                        sequence_number=self._next_sequence(context),
                        item=executor_item,
                    )
                ]

            if event_class == "ExecutorCompletedEvent":
                executor_id = getattr(event, "executor_id", "unknown")
                item_id = context.get(f"exec_item_{executor_id}", f"exec_{executor_id}_unknown")

                # Create ExecutorActionItem with completed status
                # ExecutorCompletedEvent uses 'data' field, not 'result'
                executor_item = ExecutorActionItem(
                    type="executor_action",
                    id=item_id,
                    executor_id=executor_id,
                    status="completed",
                    result=getattr(event, "data", None),
                )

                # Use our custom event type
                return [
                    CustomResponseOutputItemDoneEvent(
                        type="response.output_item.done",
                        output_index=context.get("output_index", 0),
                        sequence_number=self._next_sequence(context),
                        item=executor_item,
                    )
                ]

            if event_class == "ExecutorFailedEvent":
                executor_id = getattr(event, "executor_id", "unknown")
                item_id = context.get(f"exec_item_{executor_id}", f"exec_{executor_id}_unknown")
                error_info = getattr(event, "error", None)

                # Create ExecutorActionItem with failed status
                executor_item = ExecutorActionItem(
                    type="executor_action",
                    id=item_id,
                    executor_id=executor_id,
                    status="failed",
                    error={"message": str(error_info)} if error_info else None,
                )

                # Use our custom event type
                return [
                    CustomResponseOutputItemDoneEvent(
                        type="response.output_item.done",
                        output_index=context.get("output_index", 0),
                        sequence_number=self._next_sequence(context),
                        item=executor_item,
                    )
                ]

            # Handle informational workflow events (status, warnings, errors)
            if event_class in ["WorkflowStatusEvent", "WorkflowWarningEvent", "WorkflowErrorEvent", "RequestInfoEvent"]:
                # These are informational events that don't map to OpenAI lifecycle events
                # Convert them to trace events for debugging visibility
                event_data: dict[str, Any] = {}

                # Extract relevant data based on event type
                if event_class == "WorkflowStatusEvent":
                    event_data["state"] = str(getattr(event, "state", "unknown"))
                elif event_class == "WorkflowWarningEvent":
                    event_data["message"] = str(getattr(event, "message", ""))
                elif event_class == "WorkflowErrorEvent":
                    event_data["message"] = str(getattr(event, "message", ""))
                    event_data["error"] = str(getattr(event, "error", ""))
                elif event_class == "RequestInfoEvent":
                    request_info = getattr(event, "data", {})
                    event_data["request_info"] = request_info if isinstance(request_info, dict) else str(request_info)

                # Create a trace event for debugging
                trace_event = ResponseTraceEventComplete(
                    type="response.trace.complete",
                    data={
                        "trace_type": "workflow_info",
                        "event_type": event_class,
                        "data": event_data,
                        "timestamp": datetime.now().isoformat(),
                    },
                    span_id=f"workflow_info_{uuid4().hex[:8]}",
                    item_id=context["item_id"],
                    output_index=context.get("output_index", 0),
                    sequence_number=self._next_sequence(context),
                )

                return [trace_event]

            # For unknown/legacy events, still emit as workflow event for backward compatibility
            # Get event data and serialize if it's a SerializationMixin
            raw_event_data = getattr(event, "data", None)
            serialized_event_data: dict[str, Any] | str | None = raw_event_data
            if raw_event_data is not None and hasattr(raw_event_data, "to_dict"):
                # SerializationMixin objects - convert to dict for JSON serialization
                try:
                    serialized_event_data = raw_event_data.to_dict()
                except Exception as e:
                    logger.debug(f"Failed to serialize event data with to_dict(): {e}")
                    serialized_event_data = str(raw_event_data)

            # Create structured workflow event (keeping for backward compatibility)
            workflow_event = ResponseWorkflowEventComplete(
                type="response.workflow_event.complete",
                data={
                    "event_type": event.__class__.__name__,
                    "data": serialized_event_data,
                    "executor_id": getattr(event, "executor_id", None),
                    "timestamp": datetime.now().isoformat(),
                },
                executor_id=getattr(event, "executor_id", None),
                item_id=context["item_id"],
                output_index=context["output_index"],
                sequence_number=self._next_sequence(context),
            )

            logger.debug(f"Unhandled workflow event type: {event_class}, emitting as legacy workflow event")
            return [workflow_event]

        except Exception as e:
            logger.warning(f"Error converting workflow event: {e}")
            return [await self._create_error_event(str(e), context)]

    # Content type mappers - implementing our comprehensive mapping plan

    async def _map_text_content(self, content: Any, context: dict[str, Any]) -> ResponseTextDeltaEvent:
        """Map TextContent to ResponseTextDeltaEvent."""
        return self._create_text_delta_event(content.text, context)

    async def _map_reasoning_content(self, content: Any, context: dict[str, Any]) -> ResponseReasoningTextDeltaEvent:
        """Map TextReasoningContent to ResponseReasoningTextDeltaEvent."""
        return ResponseReasoningTextDeltaEvent(
            type="response.reasoning_text.delta",
            delta=content.text,
            item_id=context["item_id"],
            output_index=context["output_index"],
            content_index=context["content_index"],
            sequence_number=self._next_sequence(context),
        )

    async def _map_function_call_content(
        self, content: Any, context: dict[str, Any]
    ) -> list[ResponseFunctionCallArgumentsDeltaEvent | ResponseOutputItemAddedEvent]:
        """Map FunctionCallContent to OpenAI events following Responses API spec.

        Agent Framework emits FunctionCallContent in two patterns:
        1. First event: call_id + name + empty/no arguments
        2. Subsequent events: empty call_id/name + argument chunks

        We emit:
        1. response.output_item.added (with full metadata) for the first event
        2. response.function_call_arguments.delta (referencing item_id) for chunks
        """
        events: list[ResponseFunctionCallArgumentsDeltaEvent | ResponseOutputItemAddedEvent] = []

        # CASE 1: New function call (has call_id and name)
        # This is the first event that establishes the function call
        if content.call_id and content.name:
            # Use call_id as item_id (simpler, and call_id uniquely identifies the call)
            item_id = content.call_id

            # Track this function call for later argument deltas
            context["active_function_calls"][content.call_id] = {
                "item_id": item_id,
                "name": content.name,
                "arguments_chunks": [],
            }

            logger.debug(f"New function call: {content.name} (call_id={content.call_id})")

            # Emit response.output_item.added event per OpenAI spec
            events.append(
                ResponseOutputItemAddedEvent(
                    type="response.output_item.added",
                    item=ResponseFunctionToolCall(
                        id=content.call_id,  # Use call_id as the item id
                        call_id=content.call_id,
                        name=content.name,
                        arguments="",  # Empty initially, will be filled by deltas
                        type="function_call",
                        status="in_progress",
                    ),
                    output_index=context["output_index"],
                    sequence_number=self._next_sequence(context),
                )
            )

        # CASE 2: Argument deltas (content has arguments, possibly without call_id/name)
        if content.arguments:
            # Find the active function call for these arguments
            active_call = self._get_active_function_call(content, context)

            if active_call:
                item_id = active_call["item_id"]

                # Convert arguments to string if it's a dict (Agent Framework may send either)
                delta_str = content.arguments if isinstance(content.arguments, str) else json.dumps(content.arguments)

                # Emit argument delta referencing the item_id
                events.append(
                    ResponseFunctionCallArgumentsDeltaEvent(
                        type="response.function_call_arguments.delta",
                        delta=delta_str,
                        item_id=item_id,
                        output_index=context["output_index"],
                        sequence_number=self._next_sequence(context),
                    )
                )

                # Track chunk for debugging
                active_call["arguments_chunks"].append(delta_str)
            else:
                logger.warning(f"Received function call arguments without active call: {content.arguments[:50]}...")

        return events

    def _get_active_function_call(self, content: Any, context: dict[str, Any]) -> dict[str, Any] | None:
        """Find the active function call for this content.

        Uses call_id if present, otherwise falls back to most recent call.
        Necessary because Agent Framework may send argument chunks without call_id.

        Args:
            content: FunctionCallContent with possible call_id
            context: Conversion context with active_function_calls

        Returns:
            Active call dict or None
        """
        active_calls: dict[str, dict[str, Any]] = context["active_function_calls"]

        # If content has call_id, use it to find the exact call
        if hasattr(content, "call_id") and content.call_id:
            result = active_calls.get(content.call_id)
            return result if result is not None else None

        # Otherwise, use the most recent call (last one added)
        # This handles the case where Agent Framework sends argument chunks
        # without call_id in subsequent events
        if active_calls:
            return list(active_calls.values())[-1]

        return None

    async def _map_function_result_content(
        self, content: Any, context: dict[str, Any]
    ) -> ResponseFunctionResultComplete:
        """Map FunctionResultContent to DevUI custom event.

        DevUI extension: The OpenAI Responses API doesn't stream function execution results
        (in OpenAI's model, the application executes functions, not the API).
        """
        # Get call_id from content
        call_id = getattr(content, "call_id", None)
        if not call_id:
            call_id = f"call_{uuid.uuid4().hex[:8]}"

        # Extract result
        result = getattr(content, "result", None)
        exception = getattr(content, "exception", None)

        # Convert result to string, handling nested Content objects from MCP tools
        if isinstance(result, str):
            output = result
        elif result is not None:
            # Recursively serialize any nested Content objects (e.g., from MCP tools)
            serialized = _serialize_content_recursive(result)
            # Convert to JSON string if still not a string
            output = serialized if isinstance(serialized, str) else json.dumps(serialized)
        else:
            output = ""

        # Determine status based on exception
        status = "incomplete" if exception else "completed"

        # Generate item_id
        item_id = f"item_{uuid.uuid4().hex[:8]}"

        # Return DevUI custom event
        return ResponseFunctionResultComplete(
            type="response.function_result.complete",
            call_id=call_id,
            output=output,
            status=status,
            item_id=item_id,
            output_index=context["output_index"],
            sequence_number=self._next_sequence(context),
            timestamp=datetime.now().isoformat(),
        )

    async def _map_error_content(self, content: Any, context: dict[str, Any]) -> ResponseErrorEvent:
        """Map ErrorContent to ResponseErrorEvent."""
        return ResponseErrorEvent(
            type="error",
            message=getattr(content, "message", "Unknown error"),
            code=getattr(content, "error_code", None),
            param=None,
            sequence_number=self._next_sequence(context),
        )

    async def _map_usage_content(self, content: Any, context: dict[str, Any]) -> None:
        """Accumulate usage data for final Response.usage field.

        OpenAI does NOT stream usage events. Usage appears only in final Response.
        This method accumulates usage data per request for later inclusion in Response.usage.

        Returns:
            None - no event emitted (usage goes in final Response.usage)
        """
        # Extract usage from UsageContent.details (UsageDetails object)
        details = getattr(content, "details", None)
        total_tokens = getattr(details, "total_token_count", 0) or 0
        prompt_tokens = getattr(details, "input_token_count", 0) or 0
        completion_tokens = getattr(details, "output_token_count", 0) or 0

        # Accumulate for final Response.usage
        request_id = context.get("request_id", "default")
        if request_id not in self._usage_accumulator:
            self._usage_accumulator[request_id] = {"input_tokens": 0, "output_tokens": 0, "total_tokens": 0}

        self._usage_accumulator[request_id]["input_tokens"] += prompt_tokens
        self._usage_accumulator[request_id]["output_tokens"] += completion_tokens
        self._usage_accumulator[request_id]["total_tokens"] += total_tokens

        logger.debug(f"Accumulated usage for {request_id}: {self._usage_accumulator[request_id]}")

        # NO EVENT RETURNED - usage goes in final Response only
        return

    async def _map_data_content(self, content: Any, context: dict[str, Any]) -> ResponseTraceEventComplete:
        """Map DataContent to structured trace event."""
        return ResponseTraceEventComplete(
            type="response.trace.complete",
            data={
                "content_type": "data",
                "data": getattr(content, "data", None),
                "mime_type": getattr(content, "mime_type", "application/octet-stream"),
                "size_bytes": len(str(getattr(content, "data", ""))) if getattr(content, "data", None) else 0,
                "timestamp": datetime.now().isoformat(),
            },
            item_id=context["item_id"],
            output_index=context["output_index"],
            sequence_number=self._next_sequence(context),
        )

    async def _map_uri_content(self, content: Any, context: dict[str, Any]) -> ResponseTraceEventComplete:
        """Map UriContent to structured trace event."""
        return ResponseTraceEventComplete(
            type="response.trace.complete",
            data={
                "content_type": "uri",
                "uri": getattr(content, "uri", ""),
                "mime_type": getattr(content, "mime_type", "text/plain"),
                "timestamp": datetime.now().isoformat(),
            },
            item_id=context["item_id"],
            output_index=context["output_index"],
            sequence_number=self._next_sequence(context),
        )

    async def _map_hosted_file_content(self, content: Any, context: dict[str, Any]) -> ResponseTraceEventComplete:
        """Map HostedFileContent to structured trace event."""
        return ResponseTraceEventComplete(
            type="response.trace.complete",
            data={
                "content_type": "hosted_file",
                "file_id": getattr(content, "file_id", "unknown"),
                "timestamp": datetime.now().isoformat(),
            },
            item_id=context["item_id"],
            output_index=context["output_index"],
            sequence_number=self._next_sequence(context),
        )

    async def _map_hosted_vector_store_content(
        self, content: Any, context: dict[str, Any]
    ) -> ResponseTraceEventComplete:
        """Map HostedVectorStoreContent to structured trace event."""
        return ResponseTraceEventComplete(
            type="response.trace.complete",
            data={
                "content_type": "hosted_vector_store",
                "vector_store_id": getattr(content, "vector_store_id", "unknown"),
                "timestamp": datetime.now().isoformat(),
            },
            item_id=context["item_id"],
            output_index=context["output_index"],
            sequence_number=self._next_sequence(context),
        )

    async def _map_approval_request_content(self, content: Any, context: dict[str, Any]) -> dict[str, Any]:
        """Map FunctionApprovalRequestContent to custom event."""
        # Parse arguments to ensure they're always a dict, not a JSON string
        # This prevents double-escaping when the frontend calls JSON.stringify()
        arguments: dict[str, Any] = {}
        if hasattr(content, "function_call"):
            if hasattr(content.function_call, "parse_arguments"):
                # Use parse_arguments() to convert string arguments to dict
                arguments = content.function_call.parse_arguments() or {}
            else:
                # Fallback to direct access if parse_arguments doesn't exist
                arguments = getattr(content.function_call, "arguments", {})

        return {
            "type": "response.function_approval.requested",
            "request_id": getattr(content, "id", "unknown"),
            "function_call": {
                "id": getattr(content.function_call, "call_id", "") if hasattr(content, "function_call") else "",
                "name": getattr(content.function_call, "name", "") if hasattr(content, "function_call") else "",
                "arguments": arguments,
            },
            "item_id": context["item_id"],
            "output_index": context["output_index"],
            "sequence_number": self._next_sequence(context),
        }

    async def _map_approval_response_content(self, content: Any, context: dict[str, Any]) -> dict[str, Any]:
        """Map FunctionApprovalResponseContent to custom event."""
        return {
            "type": "response.function_approval.responded",
            "request_id": getattr(content, "request_id", "unknown"),
            "approved": getattr(content, "approved", False),
            "item_id": context["item_id"],
            "output_index": context["output_index"],
            "sequence_number": self._next_sequence(context),
        }

    # Helper methods

    def _create_text_delta_event(self, text: str, context: dict[str, Any]) -> ResponseTextDeltaEvent:
        """Create a ResponseTextDeltaEvent."""
        return ResponseTextDeltaEvent(
            type="response.output_text.delta",
            item_id=context["item_id"],
            output_index=context["output_index"],
            content_index=context["content_index"],
            delta=text,
            sequence_number=self._next_sequence(context),
            logprobs=[],
        )

    async def _create_error_event(self, message: str, context: dict[str, Any]) -> ResponseErrorEvent:
        """Create a ResponseErrorEvent."""
        return ResponseErrorEvent(
            type="error", message=message, code=None, param=None, sequence_number=self._next_sequence(context)
        )

    async def _create_unknown_event(self, event_data: Any, context: dict[str, Any]) -> ResponseStreamEvent:
        """Create event for unknown event types."""
        text = f"Unknown event: {event_data!s}\n"
        return self._create_text_delta_event(text, context)

    async def _create_unknown_content_event(self, content: Any, context: dict[str, Any]) -> ResponseStreamEvent:
        """Create event for unknown content types."""
        content_type = content.__class__.__name__
        text = f"Warning: Unknown content type: {content_type}\n"
        return self._create_text_delta_event(text, context)

    async def _create_error_response(self, error_message: str, request: AgentFrameworkRequest) -> OpenAIResponse:
        """Create error response."""
        error_text = f"Error: {error_message}"

        response_output_text = ResponseOutputText(type="output_text", text=error_text, annotations=[])

        response_output_message = ResponseOutputMessage(
            type="message",
            role="assistant",
            content=[response_output_text],
            id=f"msg_{uuid.uuid4().hex[:8]}",
            status="completed",
        )

        usage = ResponseUsage(
            input_tokens=0,
            output_tokens=0,
            total_tokens=0,
            input_tokens_details=InputTokensDetails(cached_tokens=0),
            output_tokens_details=OutputTokensDetails(reasoning_tokens=0),
        )

        return OpenAIResponse(
            id=f"resp_{uuid.uuid4().hex[:12]}",
            object="response",
            created_at=datetime.now().timestamp(),
            model=request.model,
            output=[response_output_message],
            usage=usage,
            parallel_tool_calls=False,
            tool_choice="none",
            tools=[],
        )
