// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Mapping;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.AI.Agents.Runtime;
using System.Text.Json.Serialization.Metadata;

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "<Pending>")]
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;
            var response = httpContext.Response;
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache,no-store";
            response.Headers.Connection = "keep-alive";

            // Make sure we disable all response buffering for SSE.
            response.Headers.ContentEncoding = "identity";
            httpContext.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();
            await response.Body.FlushAsync(cancellationToken);

            var sequenceNumber = 1;
            StreamingResponse responseChunk;
            var streamingResponseEventTypeInfo = OpenAIResponsesJsonUtilities.DefaultOptions.GetTypeInfo(typeof(StreamingResponse));

            var responseHandle = await agentProxy.RunCoreAsync(chatMessages, threadId: thread.ConversationId!, cancellationToken);
            var agentRunResponseUpdateTypeInfo = AgentAbstractionsJsonUtilities.DefaultOptions.GetTypeInfo(typeof(AgentRunResponseUpdate));
            await foreach (var update in responseHandle.WatchUpdatesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (update.Status is RequestStatus.Failed)
                {
                    throw new InvalidOperationException($"The agent run request failed: {update.Data}");
                }

                // response update representation as in AgentFramework
                var responseUpdate = (AgentRunResponseUpdate)update.Data.Deserialize(agentRunResponseUpdateTypeInfo)!;

                // OpenAI Responses server events should be represented in a specific format
                // https://platform.openai.com/docs/api-reference/responses-streaming/response

                // before sending the first update, we should send a "response.created" event
                if (sequenceNumber == 1)
                {
                    responseChunk = GiveNextStreamingResponseChunk(StreamingResponseType.Created);
                    await SendDataAsync(responseChunk, streamingResponseEventTypeInfo);
                }

                if (update.Status is RequestStatus.Completed)
                {
                    responseChunk = GiveNextStreamingResponseChunk(StreamingResponseType.Completed);
                    await SendDataAsync(responseChunk, streamingResponseEventTypeInfo);
                    break;
                }

                // send all other responses
                responseChunk = GiveNextStreamingResponseChunk(StreamingResponseType.InProgress);
                await SendDataAsync(responseChunk, streamingResponseEventTypeInfo);

                StreamingResponse GiveNextStreamingResponseChunk(StreamingResponseType type) => new()
                {
                    Type = type,
                    SequenceNumber = sequenceNumber++,
                    Response = responseHandle.ToOpenAIResponse(responseUpdate, thread, options)
                };
            }

            async ValueTask SendDataAsync<T>(T data, JsonTypeInfo typeInfo)
            {
                var eventData = JsonSerializer.Serialize(data, typeInfo);
                var eventText = $"data: {eventData}\n\n";
                await response.WriteAsync(eventText, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
