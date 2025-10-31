﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows.Declarative.Events;
using Microsoft.Agents.AI.Workflows.Declarative.Extensions;
using Microsoft.Agents.AI.Workflows.Declarative.Interpreter;
using Microsoft.Agents.AI.Workflows.Declarative.Kit;
using Microsoft.Agents.AI.Workflows.Declarative.PowerFx;
using Microsoft.Bot.ObjectModel;
using Microsoft.Bot.ObjectModel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Agents.AI.Workflows.Declarative.ObjectModel;

internal sealed class InvokeAzureAgentExecutor(InvokeAzureAgent model, WorkflowAgentProvider agentProvider, WorkflowFormulaState state) :
    DeclarativeActionExecutor<InvokeAzureAgent>(model, state)
{
    public static class Steps
    {
        public static string UserInput(string id) => $"{id}_{nameof(UserInput)}";
        public static string FunctionTool(string id) => $"{id}_{nameof(FunctionTool)}";
        public static string Resume(string id) => $"{id}_{nameof(Resume)}";
    }

    public static bool RequiresFunctionCall(object? message) => message is AgentFunctionToolRequest;

    public static bool RequiresUserInput(object? message) => message is UserInputRequest;

    public static bool RequiresNothing(object? message) => message is ActionExecutorResult;

    private AzureAgentUsage AgentUsage => Throw.IfNull(this.Model.Agent, $"{nameof(this.Model)}.{nameof(this.Model.Agent)}");
    private AzureAgentInput? AgentInput => this.Model.Input;
    private AzureAgentOutput? AgentOutput => this.Model.Output;

    protected override bool EmitResultEvent => false;
    protected override bool IsDiscreteAction => false;

    protected override async ValueTask<object?> ExecuteAsync(IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await this.InvokeAgentAsync(context, this.GetInputMessages(), cancellationToken).ConfigureAwait(false);

        return default;
    }

    public ValueTask ResumeAsync(IWorkflowContext context, AgentFunctionToolResponse message, CancellationToken cancellationToken) =>
        this.InvokeAgentAsync(context, [message.FunctionResults.ToChatMessage()], cancellationToken);

    public async ValueTask CompleteAsync(IWorkflowContext context, ActionExecutorResult message, CancellationToken cancellationToken)
    {
        await context.RaiseCompletionEventAsync(this.Model, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask InvokeAgentAsync(IWorkflowContext context, IEnumerable<ChatMessage>? messages, CancellationToken cancellationToken)
    {
        string? conversationId = this.GetConversationId();
        string agentName = this.GetAgentName();
        string? additionalInstructions = this.GetAdditionalInstructions();
        bool autoSend = this.GetAutoSendValue();

        bool isComplete = true;

        AgentRunResponse agentResponse = await agentProvider.InvokeAgentAsync(this.Id, context, agentName, conversationId, autoSend, additionalInstructions, messages, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(agentResponse.Text))
        {
            // Identify function calls that have no associated result.
            List<UserInputRequestContent> inputRequests = GetUserInputRequests(agentResponse);
            if (inputRequests.Count > 0)
            {
                isComplete = false;
                UserInputRequest approvalRequest = new(agentName, inputRequests.OfType<AIContent>().ToArray());
                await context.SendMessageAsync(approvalRequest, cancellationToken).ConfigureAwait(false);
            }

            // Identify function calls that have no associated result.
            List<FunctionCallContent> functionCalls = GetOrphanedFunctionCalls(agentResponse);
            if (functionCalls.Count > 0)
            {
                isComplete = false;
                AgentFunctionToolRequest toolRequest = new(agentName, functionCalls);
                await context.SendMessageAsync(toolRequest, cancellationToken).ConfigureAwait(false);
            }
        }

        if (isComplete)
        {
            await context.SendResultMessageAsync(this.Id, result: null, cancellationToken).ConfigureAwait(false);
        }

        await this.AssignAsync(this.AgentOutput?.Messages?.Path, agentResponse.Messages.ToTable(), context).ConfigureAwait(false);
    }

    private IEnumerable<ChatMessage>? GetInputMessages()
    {
        DataValue? userInput = null;

        if (this.AgentInput?.Messages is not null)
        {
            EvaluationResult<DataValue> expressionResult = this.Evaluator.GetValue(this.AgentInput.Messages);
            userInput = expressionResult.Value;
        }

        return userInput?.ToChatMessages();
    }

    private static List<FunctionCallContent> GetOrphanedFunctionCalls(AgentRunResponse agentResponse)
    {
        HashSet<string> functionResultIds =
            [.. agentResponse.Messages
                    .SelectMany(
                        m =>
                            m.Contents
                                .OfType<FunctionResultContent>()
                                .Select(functionCall => functionCall.CallId))];

        List<FunctionCallContent> functionCalls = [];
        foreach (FunctionCallContent functionCall in agentResponse.Messages.SelectMany(m => m.Contents.OfType<FunctionCallContent>()))
        {
            if (!functionResultIds.Contains(functionCall.CallId))
            {
                functionCalls.Add(functionCall);
            }
        }

        return functionCalls;
    }

    private static List<UserInputRequestContent> GetUserInputRequests(AgentRunResponse agentResponse) =>
        agentResponse.Messages.SelectMany(m => m.Contents.OfType<UserInputRequestContent>()).ToList();

    private string? GetConversationId()
    {
        if (this.Model.ConversationId is null)
        {
            return null;
        }

        EvaluationResult<string> conversationIdResult = this.Evaluator.GetValue(this.Model.ConversationId);
        return conversationIdResult.Value.Length == 0 ? null : conversationIdResult.Value;
    }

    private string GetAgentName() =>
        this.Evaluator.GetValue(
            Throw.IfNull(
                this.AgentUsage.Name,
                $"{nameof(this.Model)}.{nameof(this.Model.Agent)}.{nameof(this.Model.Agent.Name)}")).Value;

    private string? GetAdditionalInstructions()
    {
        string? additionalInstructions = null;

        if (this.AgentInput?.AdditionalInstructions is not null)
        {
            additionalInstructions = this.Engine.Format(this.AgentInput.AdditionalInstructions);
        }

        return additionalInstructions;
    }

    private bool GetAutoSendValue()
    {
        if (this.AgentOutput?.AutoSend is null)
        {
            return true;
        }

        EvaluationResult<bool> autoSendResult = this.Evaluator.GetValue(this.AgentOutput.AutoSend);

        return autoSendResult.Value;
    }
}
