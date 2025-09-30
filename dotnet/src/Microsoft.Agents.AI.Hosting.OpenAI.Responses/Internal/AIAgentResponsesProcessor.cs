// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Hosting.Responses.Mapping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;

namespace Microsoft.Agents.AI.Hosting.Responses.Internal;

/// <summary>
/// OpenAI Responses processor associated with a specific <see cref="AIAgent"/>.
/// </summary>
internal class AIAgentResponsesProcessor
{
#pragma warning disable IDE0052 // Remove unread private members
    private readonly ILogger _logger;
#pragma warning restore IDE0052 // Remove unread private members
    private readonly AIAgent _agent;

    private string AgentName => this._agent.Name!;

    public AIAgentResponsesProcessor(AIAgent agent, ILoggerFactory? loggerFactory = null)
    {
        this._logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AIAgentResponsesProcessor>();
        this._agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public async Task<IResult> CreateModelResponseAsync(CreateResponse createResponse, CancellationToken cancellationToken)
    {
        // No API exists to load the agentThread from threadId at the moment
        _ = createResponse.Conversation?.ConversationId ?? $"conv_{Guid.NewGuid():N}";
        AgentThread? agentThread = null!; // this._agent.GetNewThread(conversationId);

        var options = new OpenAIResponsesRunOptions();
        var chatMessages = createResponse.Input.ToChatMessages();

        //if (createResponse.Stream)
        //{
        //    return new OpenAIStreamingResponsesResult(this._agent, chatMessages, agentThread, options);
        //}

        var agentResponse = await this._agent.RunAsync(chatMessages, agentThread, options, cancellationToken).ConfigureAwait(false);
        var openAIResponse = agentResponse.ToOpenAIResponse(this.AgentName, agentThread, options);
        return Results.Ok(openAIResponse);
    }

    private class OpenAIStreamingResponsesResult(
        AIAgent agent,
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

        private async IAsyncEnumerable<SseItem<StreamingResponseUpdate>> GetStreamingResponsesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string eventType;
            var sequenceNumber = 1;
            AgentThread? agentThread = null!;

            var agentRunResponseUpdateTypeInfo = AgentAbstractionsJsonUtilities.DefaultOptions.GetTypeInfo(typeof(AgentRunResponseUpdate));
            await foreach (var update in agent.RunStreamingAsync(chatMessages, thread: agentThread, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (sequenceNumber == 1)
                {
                    var createdChunk = CreateChunk(StreamingResponseType.Created, out eventType);
                    yield return new SseItem<StreamingResponseInProgressUpdate>(createdChunk, eventType);
                }

                if (update.Status is RequestStatus.Completed)
                {
                    var completedChunk = CreateChunk(StreamingResponseType.Completed, out eventType);
                    yield return new SseItem<StreamingResponse>(completedChunk, eventType);
                    break;
                }

                var inProgressChunk = CreateChunk(StreamingResponseType.InProgress, out eventType);
                yield return new SseItem<StreamingResponse>(inProgressChunk, eventType);

                StreamingResponse CreateChunk(StreamingResponseType type, out string eventType)
                {
                    eventType = type.ToEventName();
                    return new()
                    {
                        Type = type,
                        SequenceNumber = sequenceNumber++,
                        Response = responseHandle.ToOpenAIResponse(responseUpdate, thread, options)
                    };
                }
            }
        }
    }
}
