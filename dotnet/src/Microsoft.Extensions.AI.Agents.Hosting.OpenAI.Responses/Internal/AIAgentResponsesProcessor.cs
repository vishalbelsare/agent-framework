// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal class AIAgentResponsesProcessor
{
    private readonly ILogger _logger;
    private readonly AgentProxy _agentProxy;

    public AIAgentResponsesProcessor(AgentProxy agentProxy, ILoggerFactory? loggerFactory = null)
    {
        this._logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AIAgentResponsesProcessor>();
        this._agentProxy = agentProxy ?? throw new ArgumentNullException(nameof(agentProxy));
    }

    public Task<Model.OpenAIResponse> CreateModelResponseAsync(CreateResponse createResponse, CancellationToken cancellationToken)
    {
        var conversationId = createResponse.Conversation.GetConversationId();
        var agentThread = conversationId is not null ? this._agentProxy.GetThread(conversationId) : this._agentProxy.GetNewThread();

        var options = new OpenAIResponsesRunOptions();

        return createResponse.Stream
            ? this.HandleStreamingResponseAsync(createResponse, agentThread, options, cancellationToken)
            : this.HandleNonStreamingResponseAsync(createResponse, agentThread, options, cancellationToken);
    }

    public Task<Model.OpenAIResponse> GetModelResponseAsync(string responseId, string? includeObfuscation, string? startingAfter, bool stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteModelResponseAsync(string responseId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Model.OpenAIResponse> CancelResponseAsync(string responseId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IList<ResponseItem>> ListInputItemsAsync(string responseId, string? after, IList<IncludeParameter>? include, int? limit, string? order, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task<Model.OpenAIResponse> HandleStreamingResponseAsync(
        CreateResponse createResponse,
        AgentThread thread,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task<Model.OpenAIResponse> HandleNonStreamingResponseAsync(
        CreateResponse createResponse,
        AgentThread thread,
        AgentRunOptions options,
        CancellationToken cancellationToken)
    {
        var chatMessages = createResponse.Input.

        var agentResponse = await this._agentProxy.RunAsync(messages, thread, options, cancellationToken).ConfigureAwait(false);
        return agentResponse.ToOpenAIResponse();
    }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
