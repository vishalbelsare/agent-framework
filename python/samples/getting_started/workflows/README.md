# Workflows Getting Started Samples

## Installation

Microsoft Agent Framework Workflows support ships with the core `agent-framework` or `agent-framework-core` package, so no extra installation step is required.

To install with visualization support:

```bash
pip install agent-framework[viz] --pre
```

To export visualization images you also need to [install GraphViz](https://graphviz.org/download/).

## Samples Overview

## Foundational Concepts - Start Here

Begin with the `_start-here` folder in order. These three samples introduce the core ideas of executors, edges, agents in workflows, and streaming.

| Sample | File | Concepts |
|--------|------|----------|
| Executors and Edges | [_start-here/step1_executors_and_edges.py](./_start-here/step1_executors_and_edges.py) | Minimal workflow with basic executors and edges |
| Agents in a Workflow | [_start-here/step2_agents_in_a_workflow.py](./_start-here/step2_agents_in_a_workflow.py) | Introduces adding Agents as nodes; calling agents inside a workflow |
| Streaming (Basics) | [_start-here/step3_streaming.py](./_start-here/step3_streaming.py) | Extends workflows with event streaming |

Once comfortable with these, explore the rest of the samples below.

---

## Samples Overview (by directory)

### agents

| Sample | File | Concepts |
|---|---|---|
| Azure Chat Agents (Streaming) | [agents/azure_chat_agents_streaming.py](./agents/azure_chat_agents_streaming.py) | Add Azure Chat agents as edges and handle streaming events |
| Azure AI Chat Agents (Streaming) | [agents/azure_ai_agents_streaming.py](./agents/azure_ai_agents_streaming.py) | Add Azure AI agents as edges and handle streaming events |
| Azure Chat Agents (Function Bridge) | [agents/azure_chat_agents_function_bridge.py](./agents/azure_chat_agents_function_bridge.py) | Chain two agents with a function executor that injects external context |
| Azure Chat Agents (Tools + HITL) | [agents/azure_chat_agents_tool_calls_with_feedback.py](./agents/azure_chat_agents_tool_calls_with_feedback.py) | Tool-enabled writer/editor pipeline with human feedback gating |
| Custom Agent Executors | [agents/custom_agent_executors.py](./agents/custom_agent_executors.py) | Create executors to handle agent run methods |
| Sequential Workflow as Agent | [agents/sequential_workflow_as_agent.py](./agents/sequential_workflow_as_agent.py) | Build a sequential workflow orchestrating agents, then expose it as a reusable agent |
| Concurrent Workflow as Agent | [agents/concurrent_workflow_as_agent.py](./agents/concurrent_workflow_as_agent.py) | Build a concurrent fan-out/fan-in workflow, then expose it as a reusable agent |
| Magentic Workflow as Agent | [agents/magentic_workflow_as_agent.py](./agents/magentic_workflow_as_agent.py) | Configure Magentic orchestration with callbacks, then expose the workflow as an agent |
| Workflow as Agent (Reflection Pattern) | [agents/workflow_as_agent_reflection_pattern.py](./agents/workflow_as_agent_reflection_pattern.py) | Wrap a workflow so it can behave like an agent (reflection pattern) |
| Workflow as Agent + HITL | [agents/workflow_as_agent_human_in_the_loop.py](./agents/workflow_as_agent_human_in_the_loop.py) | Extend workflow-as-agent with human-in-the-loop capability |

### checkpoint

| Sample | File | Concepts |
|---|---|---|
| Checkpoint & Resume | [checkpoint/checkpoint_with_resume.py](./checkpoint/checkpoint_with_resume.py) | Create checkpoints, inspect them, and resume execution |
| Checkpoint & HITL Resume | [checkpoint/checkpoint_with_human_in_the_loop.py](./checkpoint/checkpoint_with_human_in_the_loop.py) | Combine checkpointing with human approvals and resume pending HITL requests |
| Checkpointed Sub-Workflow | [checkpoint/sub_workflow_checkpoint.py](./checkpoint/sub_workflow_checkpoint.py) | Save and resume a sub-workflow that pauses for human approval |

### composition

| Sample | File | Concepts |
|---|---|---|
| Sub-Workflow (Basics) | [composition/sub_workflow_basics.py](./composition/sub_workflow_basics.py) | Wrap a workflow as an executor and orchestrate sub-workflows |
| Sub-Workflow: Request Interception | [composition/sub_workflow_request_interception.py](./composition/sub_workflow_request_interception.py) | Intercept and forward sub-workflow requests using @handler for SubWorkflowRequestMessage |
| Sub-Workflow: Parallel Requests | [composition/sub_workflow_parallel_requests.py](./composition/sub_workflow_parallel_requests.py) | Multiple specialized interceptors handling different request types from same sub-workflow |

### control-flow

| Sample | File | Concepts |
|---|---|---|
| Sequential Executors | [control-flow/sequential_executors.py](./control-flow/sequential_executors.py) | Sequential workflow with explicit executor setup |
| Sequential (Streaming) | [control-flow/sequential_streaming.py](./control-flow/sequential_streaming.py) | Stream events from a simple sequential run |
| Edge Condition | [control-flow/edge_condition.py](./control-flow/edge_condition.py) | Conditional routing based on agent classification |
| Switch-Case Edge Group | [control-flow/switch_case_edge_group.py](./control-flow/switch_case_edge_group.py) | Switch-case branching using classifier outputs |
| Multi-Selection Edge Group | [control-flow/multi_selection_edge_group.py](./control-flow/multi_selection_edge_group.py) | Select one or many targets dynamically (subset fan-out) |
| Simple Loop | [control-flow/simple_loop.py](./control-flow/simple_loop.py) | Feedback loop where an agent judges ABOVE/BELOW/MATCHED |

### human-in-the-loop

| Sample | File | Concepts |
|---|---|---|
| Human-In-The-Loop (Guessing Game) | [human-in-the-loop/guessing_game_with_human_input.py](./human-in-the-loop/guessing_game_with_human_input.py) | Interactive request/response prompts with a human |
| Azure Agents Tool Feedback Loop | [agents/azure_chat_agents_tool_calls_with_feedback.py](./agents/azure_chat_agents_tool_calls_with_feedback.py) | Two-agent workflow that streams tool calls and pauses for human guidance between passes |

### observability

| Sample | File | Concepts |
|---|---|---|
| Tracing (Basics) | [observability/tracing_basics.py](./observability/tracing_basics.py) | Use basic tracing for workflow telemetry. Refer to this [directory](../observability/) to learn more about observability concepts. |

### orchestration

| Sample | File | Concepts |
|---|---|---|
| Concurrent Orchestration (Default Aggregator) | [orchestration/concurrent_agents.py](./orchestration/concurrent_agents.py) | Fan-out to multiple agents; fan-in with default aggregator returning combined ChatMessages |
| Concurrent Orchestration (Custom Aggregator) | [orchestration/concurrent_custom_aggregator.py](./orchestration/concurrent_custom_aggregator.py) | Override aggregator via callback; summarize results with an LLM |
| Concurrent Orchestration (Custom Agent Executors) | [orchestration/concurrent_custom_agent_executors.py](./orchestration/concurrent_custom_agent_executors.py) | Child executors own ChatAgents; concurrent fan-out/fan-in via ConcurrentBuilder |
| Group Chat Orchestration with Prompt Based Manager | [orchestration/group_chat_prompt_based_manager.py](./orchestration/group_chat_prompt_based_manager.py) | LLM Manager-directed conversation using GroupChatBuilder |
| Group Chat with Simple Function Selector | [orchestration/group_chat_simple_selector.py](./orchestration/group_chat_simple_selector.py) | Group chat with a simple function selector for next speaker |
| Handoff (Simple) | [orchestration/handoff_simple.py](./orchestration/handoff_simple.py) | Single-tier routing: triage agent routes to specialists, control returns to user after each specialist response |
| Handoff (Specialist-to-Specialist) | [orchestration/handoff_specialist_to_specialist.py](./orchestration/handoff_specialist_to_specialist.py) | Multi-tier routing: specialists can hand off to other specialists using `.add_handoff()` fluent API |
| Magentic Workflow (Multi-Agent) | [orchestration/magentic.py](./orchestration/magentic.py) | Orchestrate multiple agents with Magentic manager and streaming |
| Magentic + Human Plan Review | [orchestration/magentic_human_plan_update.py](./orchestration/magentic_human_plan_update.py) | Human reviews/updates the plan before execution |
| Magentic + Checkpoint Resume | [orchestration/magentic_checkpoint.py](./orchestration/magentic_checkpoint.py) | Resume Magentic orchestration from saved checkpoints |
| Sequential Orchestration (Agents) | [orchestration/sequential_agents.py](./orchestration/sequential_agents.py) | Chain agents sequentially with shared conversation context |
| Sequential Orchestration (Custom Executor) | [orchestration/sequential_custom_executors.py](./orchestration/sequential_custom_executors.py) | Mix agents with a summarizer that appends a compact summary |

**Magentic checkpointing tip**: Treat `MagenticBuilder.participants` keys as stable identifiers. When resuming from a checkpoint, the rebuilt workflow must reuse the same participant names; otherwise the checkpoint cannot be applied and the run will fail fast.

**Handoff workflow tip**: Handoff workflows maintain the full conversation history including any
`ChatMessage.additional_properties` emitted by your agents. This ensures routing metadata remains
intact across all agent transitions. For specialist-to-specialist handoffs, use `.add_handoff(source, targets)`
to configure which agents can route to which others with a fluent, type-safe API.

### parallelism

| Sample | File | Concepts |
|---|---|---|
| Concurrent (Fan-out/Fan-in) | [parallelism/fan_out_fan_in_edges.py](./parallelism/fan_out_fan_in_edges.py) | Dispatch to multiple executors and aggregate results |
| Aggregate Results of Different Types | [parallelism/aggregate_results_of_different_types.py](./parallelism/aggregate_results_of_different_types.py) | Handle results of different types from multiple concurrent executors |
| Map-Reduce with Visualization | [parallelism/map_reduce_and_visualization.py](./parallelism/map_reduce_and_visualization.py) | Fan-out/fan-in pattern with diagram export |

### state-management

| Sample | File | Concepts |
|---|---|---|
| Shared States | [state-management/shared_states_with_agents.py](./state-management/shared_states_with_agents.py) | Store in shared state once and later reuse across agents |

### visualization

| Sample | File | Concepts |
|---|---|---|
| Concurrent with Visualization | [visualization/concurrent_with_visualization.py](./visualization/concurrent_with_visualization.py) | Fan-out/fan-in workflow with diagram export |

### resources

- Sample text inputs used by certain workflows:
  - [resources/long_text.txt](./resources/long_text.txt)
  - [resources/email.txt](./resources/email.txt)
  - [resources/spam.txt](./resources/spam.txt)
  - [resources/ambiguous_email.txt](./resources/ambiguous_email.txt)

Notes

- Agent-based samples use provider SDKs (Azure/OpenAI, etc.). Ensure credentials are configured, or adapt agents accordingly.

Sequential orchestration uses a few small adapter nodes for plumbing:

- "input-conversation" normalizes input to `list[ChatMessage]`
- "to-conversation:<participant>" converts agent responses into the shared conversation
- "complete" publishes the final `WorkflowOutputEvent`
These may appear in event streams (ExecutorInvoke/Completed). They’re analogous to
concurrent’s dispatcher and aggregator and can be ignored if you only care about agent activity.

### Environment Variables

- **AzureOpenAIChatClient**: Set Azure OpenAI environment variables as documented [here](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/chat_client/README.md#environment-variables).
  These variables are required for samples that construct `AzureOpenAIChatClient`

- **OpenAI** (used in orchestration samples):
  - [OpenAIChatClient env vars](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/agents/openai_chat_client/README.md)
  - [OpenAIResponsesClient env vars](https://github.com/microsoft/agent-framework/blob/main/python/samples/getting_started/agents/openai_responses_client/README.md)
