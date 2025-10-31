# Copyright (c) Microsoft. All rights reserved.

import asyncio

from agent_framework import (
    Executor,
    WorkflowBuilder,
    WorkflowContext,
    WorkflowOutputEvent,
    handler,
)
from agent_framework.observability import get_tracer, setup_observability
from opentelemetry.trace import SpanKind
from opentelemetry.trace.span import format_trace_id
from typing_extensions import Never

"""
This sample shows the telemetry collected when running a Agent Framework workflow.

Telemetry data that the workflow system emits includes:
- Overall workflow build & execution spans
- Individual executor processing spans
- Message publishing between executors
"""


# Executors for sequential workflow
class UpperCaseExecutor(Executor):
    """An executor that converts text to uppercase."""

    @handler
    async def to_upper_case(self, text: str, ctx: WorkflowContext[str]) -> None:
        """Execute the task by converting the input string to uppercase."""
        print(f"UpperCaseExecutor: Processing '{text}'")
        result = text.upper()
        print(f"UpperCaseExecutor: Result '{result}'")

        # Send the result to the next executor in the workflow.
        await ctx.send_message(result)


class ReverseTextExecutor(Executor):
    """An executor that reverses text."""

    @handler
    async def reverse_text(self, text: str, ctx: WorkflowContext[Never, str]) -> None:
        """Execute the task by reversing the input string."""
        print(f"ReverseTextExecutor: Processing '{text}'")
        result = text[::-1]
        print(f"ReverseTextExecutor: Result '{result}'")

        # Yield the output.
        await ctx.yield_output(result)


async def run_sequential_workflow() -> None:
    """Run a simple sequential workflow demonstrating telemetry collection.

    This workflow processes a string through two executors in sequence:
    1. UpperCaseExecutor converts the input to uppercase
    2. ReverseTextExecutor reverses the string and completes the workflow
    """
    # Step 1: Create the executors.
    upper_case_executor = UpperCaseExecutor(id="upper_case_executor")
    reverse_text_executor = ReverseTextExecutor(id="reverse_text_executor")

    # Step 2: Build the workflow with the defined edges.
    workflow = (
        WorkflowBuilder()
        .add_edge(upper_case_executor, reverse_text_executor)
        .set_start_executor(upper_case_executor)
        .build()
    )

    # Step 3: Run the workflow with an initial message.
    input_text = "hello world"
    print(f"Starting workflow with input: '{input_text}'")

    output_event = None
    async for event in workflow.run_stream("Hello world"):
        if isinstance(event, WorkflowOutputEvent):
            # The WorkflowOutputEvent contains the final result.
            output_event = event

    if output_event:
        print(f"Workflow completed with result: '{output_event.data}'")


async def main():
    """Run the telemetry sample with a simple sequential workflow."""
    # This will enable tracing and create the necessary tracing, logging and metrics providers
    # based on environment variables. See the .env.example file for the available configuration options.
    setup_observability()

    with get_tracer().start_as_current_span("Sequential Workflow Scenario", kind=SpanKind.CLIENT) as current_span:
        print(f"Trace ID: {format_trace_id(current_span.get_span_context().trace_id)}")

        # Run the sequential workflow scenario
        await run_sequential_workflow()


if __name__ == "__main__":
    asyncio.run(main())
