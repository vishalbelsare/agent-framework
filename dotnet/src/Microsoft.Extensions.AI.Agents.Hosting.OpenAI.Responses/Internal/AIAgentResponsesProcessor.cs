// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.AI.Agents.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;

/// <summary>
/// OpenAI Responses processor associated with a specific <see cref="AIAgent"/>.
/// </summary>
internal class AIAgentResponsesProcessor
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly ILogger _logger;
#pragma warning restore IDE0052 // Remove unread private members
    private readonly AgentProxy _agentProxy;

    private ActorType AgentType => new(this._agentProxy.Name);

    public AIAgentResponsesProcessor(AgentProxy agentProxy, ILoggerFactory? loggerFactory = null)
    {
        this._logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AIAgentResponsesProcessor>();
        this._agentProxy = agentProxy ?? throw new ArgumentNullException(nameof(agentProxy));
    }

    public async Task<IResult> CreateModelResponseAsync(CreateResponse createResponse, CancellationToken cancellationToken)
    {
        string conversationId = createResponse.Conversation?.ConversationId ?? $"conv_{Guid.NewGuid():N}";
        var agentThread = this._agentProxy.GetThread(conversationId);

        var options = new OpenAIResponsesRunOptions();
        var chatMessages = createResponse.Input.ToChatMessages();

        if (createResponse.Stream)
        {
            return new OpenAIStreamingResponsesResult(this._agentProxy, chatMessages, agentThread, options);
        }

        var agentResponse = await this._agentProxy.RunAsync(chatMessages, agentThread, options, cancellationToken).ConfigureAwait(false);
        var openAIResponse = agentResponse.ToOpenAIResponse(this.AgentType, agentThread, options);
        return Results.Ok(openAIResponse);
    }

    private class OpenAIStreamingResponsesResult(
        AgentProxy agentProxy,
        IEnumerable<ChatMessage> chatMessages,
        AgentThread thread,
        OpenAIResponsesRunOptions options) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;
            var response = httpContext.Response;

            // Set SSE headers
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache,no-store";
            response.Headers.Connection = "keep-alive";
            response.Headers.ContentEncoding = "identity";
            httpContext.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();

            var streamingResponseEventTypeInfo = OpenAIResponsesJsonUtilities.DefaultOptions.GetTypeInfo(typeof(StreamingResponse));

            return SseFormatter.WriteAsync(
                source: this.GetStreamingResponsesAsync(cancellationToken),
                destination: response.Body,
                itemFormatter: (sseItem, bufferWriter) =>
                {
                    var json = JsonSerializer.SerializeToUtf8Bytes(sseItem.Data, streamingResponseEventTypeInfo);
                    bufferWriter.Write(json);
                },
                cancellationToken);
        }

        private async IAsyncEnumerable<SseItem<StreamingResponse>> GetStreamingResponsesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sequenceNumber = 1;
            var responseHandle = await agentProxy.RunCoreAsync(chatMessages, threadId: thread.ConversationId!, cancellationToken).ConfigureAwait(false);
            var agentRunResponseUpdateTypeInfo = AgentAbstractionsJsonUtilities.DefaultOptions.GetTypeInfo(typeof(AgentRunResponseUpdate));

            await foreach (var update in responseHandle.WatchUpdatesAsync(cancellationToken).WithCancellation(cancellationToken))
            {
                if (update.Status is RequestStatus.Failed)
                {
                    throw new InvalidOperationException($"The agent run request failed: {update.Data}");
                }

                var responseUpdate = (AgentRunResponseUpdate)update.Data.Deserialize(agentRunResponseUpdateTypeInfo)!;

                if (sequenceNumber == 1)
                {
                    var createdChunk = CreateChunk(StreamingResponseType.Created);
                    yield return new SseItem<StreamingResponse>(createdChunk);
                }

                if (update.Status is RequestStatus.Completed)
                {
                    var completedChunk = CreateChunk(StreamingResponseType.Completed);
                    yield return new SseItem<StreamingResponse>(completedChunk);
                    break;
                }

                var inProgressChunk = CreateChunk(StreamingResponseType.InProgress);
                yield return new SseItem<StreamingResponse>(inProgressChunk);

                StreamingResponse CreateChunk(StreamingResponseType type) => new()
                {
                    Type = type,
                    SequenceNumber = sequenceNumber++,
                    Response = responseHandle.ToOpenAIResponse(responseUpdate, thread, options)
                };
            }
        }
    }
}
