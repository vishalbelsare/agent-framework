﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Agents.AI.Workflows.Declarative.Events;
using Microsoft.Extensions.AI;
using Shared.Code;
using Xunit.Sdk;

namespace Microsoft.Agents.AI.Workflows.Declarative.IntegrationTests.Framework;

internal sealed class WorkflowHarness(Workflow workflow, string runId)
{
    private CheckpointManager? _checkpointManager;
    private CheckpointInfo? LastCheckpoint { get; set; }

    public async Task<WorkflowEvents> RunTestcaseAsync<TInput>(Testcase testcase, TInput input, bool useJson = false) where TInput : notnull
    {
        WorkflowEvents workflowEvents = await this.RunWorkflowAsync(input, useJson);
        int requestCount = (workflowEvents.InputEvents.Count + 1) / 2;
        int responseCount = 0;
        while (requestCount > responseCount)
        {
            Assert.NotNull(testcase.Setup.Responses);
            Assert.NotEmpty(testcase.Setup.Responses);
            string inputText = testcase.Setup.Responses[responseCount].Value;
            Console.WriteLine($"INPUT: {inputText}");
            ++responseCount;
            WorkflowEvents runEvents = await this.ResumeAsync(new AnswerResponse(inputText)).ConfigureAwait(false);
            workflowEvents = new WorkflowEvents([.. workflowEvents.Events, .. runEvents.Events]);
            requestCount = (workflowEvents.InputEvents.Count + 1) / 2;
        }

        return workflowEvents;
    }

    public async Task<WorkflowEvents> RunWorkflowAsync<TInput>(TInput input, bool useJson = false) where TInput : notnull
    {
        Console.WriteLine("RUNNING WORKFLOW...");
        Checkpointed<StreamingRun> run = await InProcessExecution.StreamAsync(workflow, input, this.GetCheckpointManager(useJson), runId);
        IReadOnlyList<WorkflowEvent> workflowEvents = await MonitorAndDisposeWorkflowRunAsync(run).ToArrayAsync();
        this.LastCheckpoint = workflowEvents.OfType<SuperStepCompletedEvent>().LastOrDefault()?.CompletionInfo?.Checkpoint;
        return new WorkflowEvents(workflowEvents);
    }

    public async Task<WorkflowEvents> ResumeAsync(object response)
    {
        Console.WriteLine("\nRESUMING WORKFLOW...");
        Assert.NotNull(this.LastCheckpoint);
        Checkpointed<StreamingRun> run = await InProcessExecution.ResumeStreamAsync(workflow, this.LastCheckpoint, this.GetCheckpointManager(), runId);
        IReadOnlyList<WorkflowEvent> workflowEvents = await MonitorAndDisposeWorkflowRunAsync(run, response).ToArrayAsync();
        return new WorkflowEvents(workflowEvents);
    }

    public static async Task<WorkflowHarness> GenerateCodeAsync<TInput>(
        string runId,
        string workflowProviderCode,
        string workflowProviderName,
        string workflowProviderNamespace,
        DeclarativeWorkflowOptions options,
        TInput input) where TInput : notnull
    {
        // Compile the code
        Assembly assembly = Compiler.Build(workflowProviderCode, Compiler.RepoDependencies(typeof(DeclarativeWorkflowBuilder)));
        Type? type = assembly.GetType($"{workflowProviderNamespace}.{workflowProviderName}");
        Assert.NotNull(type);
        MethodInfo? method = type.GetMethod("CreateWorkflow");
        Assert.NotNull(method);
        MethodInfo genericMethod = method.MakeGenericMethod(typeof(TInput));
        object? workflowObject = genericMethod.Invoke(null, [options, null]);
        Workflow workflow = Assert.IsType<Workflow>(workflowObject);

        return new WorkflowHarness(workflow, runId);
    }

    private CheckpointManager GetCheckpointManager(bool useJson = false)
    {
        if (useJson && this._checkpointManager is null)
        {
            DirectoryInfo checkpointFolder = Directory.CreateDirectory(Path.Combine(".", $"chk-{DateTime.Now:yyMMdd-hhmmss-ff}"));
            this._checkpointManager = CheckpointManager.CreateJson(new FileSystemJsonCheckpointStore(checkpointFolder));
        }
        else
        {
            this._checkpointManager ??= CheckpointManager.CreateInMemory();
        }

        return this._checkpointManager;
    }

    private static async IAsyncEnumerable<WorkflowEvent> MonitorAndDisposeWorkflowRunAsync(Checkpointed<StreamingRun> run, object? response = null)
    {
        await using IAsyncDisposable disposeRun = run;

        await foreach (WorkflowEvent workflowEvent in run.Run.WatchStreamAsync().ConfigureAwait(false))
        {
            bool exitLoop = false;

            switch (workflowEvent)
            {
                case RequestInfoEvent requestInfo:
                    Console.WriteLine($"REQUEST #{requestInfo.Request.RequestId}");
                    if (response is not null)
                    {
                        ExternalResponse requestResponse = requestInfo.Request.CreateResponse(response);
                        await run.Run.SendResponseAsync(requestResponse).ConfigureAwait(false);
                        response = null;
                    }
                    else
                    {
                        exitLoop = true;
                    }
                    break;

                case ConversationUpdateEvent conversationEvent:
                    Console.WriteLine($"CONVERSATION: {conversationEvent.ConversationId}");
                    break;

                case ExecutorFailedEvent failureEvent:
                    Console.WriteLine($"Executor failed [{failureEvent.ExecutorId}]: {failureEvent.Data?.Message ?? "Unknown"}");
                    break;

                case WorkflowErrorEvent errorEvent:
                    throw errorEvent.Data as Exception ?? new XunitException("Unexpected failure...");

                case ExecutorInvokedEvent executorInvokeEvent:
                    Console.WriteLine($"EXEC: {executorInvokeEvent.ExecutorId}");
                    break;

                case DeclarativeActionInvokedEvent actionInvokeEvent:
                    Console.WriteLine($"ACTION: {actionInvokeEvent.ActionId} [{actionInvokeEvent.ActionType}]");
                    break;

                case AgentRunResponseEvent responseEvent:
                    if (!string.IsNullOrEmpty(responseEvent.Response.Text))
                    {
                        Console.WriteLine($"AGENT: {responseEvent.Response.AgentId}: {responseEvent.Response.Text}");
                    }
                    else
                    {
                        foreach (FunctionCallContent toolCall in responseEvent.Response.Messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>()))
                        {
                            Console.WriteLine($"TOOL: {toolCall.Name} [{responseEvent.Response.AgentId}]");
                        }
                    }
                    break;
            }

            yield return workflowEvent;

            if (exitLoop)
            {
                break;
            }
        }

        Console.WriteLine("SUSPENDING WORKFLOW...\n");
    }
}
