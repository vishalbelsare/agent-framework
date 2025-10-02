// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Buffers;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Extensions;
using Microsoft.Agents.AI.Hosting.Responses.Model;
using Microsoft.Agents.AI.Hosting.Responses.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.AI;
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

    public AIAgentResponsesProcessor(AIAgent agent, ILoggerFactory? loggerFactory = null)
    {
        this._logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AIAgentResponsesProcessor>();
        this._agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public async Task<IResult> CreateModelResponseAsync(ResponseCreationOptions responseCreationOptions, CancellationToken cancellationToken)
    {
        var options = new OpenAIResponsesRunOptions();
        AgentThread? agentThread = null!; // not supported to resolve from conversationId

        var inputItems = responseCreationOptions.GetInput();
        var chatMessages = inputItems.AsChatMessages();

        if (responseCreationOptions.GetStream())
        {
            return new OpenAIStreamingResponsesResult(this._agent, chatMessages);
        }

        var agentResponse = await this._agent.RunAsync(chatMessages, agentThread, options, cancellationToken).ConfigureAwait(false);
        return new OpenAIResponseResult(agentResponse);
    }

    private class OpenAIResponseResult(AgentRunResponse agentResponse) : IResult
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Otherwise reports on await using var writer.")]
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            // note: OpenAI SDK types provide their own serialization implementation
            // so we cant simply return IResult wrap for the typed-object.
            // instead writing to the response body can be done.

            var cancellationToken = httpContext.RequestAborted;
            var response = httpContext.Response;

            var chatResponse = agentResponse.AsChatResponse();
            var openAIResponse = chatResponse.AsOpenAIResponse();
            var openAIResponseJsonModel = openAIResponse as IJsonModel<OpenAIResponse>;
            Debug.Assert(openAIResponseJsonModel is not null);

            await using var writer = new Utf8JsonWriter(response.BodyWriter, new JsonWriterOptions { SkipValidation = false });
            openAIResponseJsonModel.Write(writer, ModelReaderWriterOptions.Json);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private class OpenAIStreamingResponsesResult(
        AIAgent agent,
        IEnumerable<ChatMessage> chatMessages) : IResult
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
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

            return SseFormatter.WriteAsync(
                source: this.GetStreamingResponsesAsync(cancellationToken),
                destination: response.Body,
                itemFormatter: (sseItem, bufferWriter) =>
                {
                    var json = JsonSerializer.SerializeToUtf8Bytes(sseItem.Data, sseItem.Data.GetType(), OpenAIResponsesJsonUtilities.DefaultOptions);
                    bufferWriter.Write(json);
                },
                cancellationToken);
        }

        private async IAsyncEnumerable<SseItem<StreamingResponseEventBase>> GetStreamingResponsesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sequenceNumber = 1;
            var outputIndex = 1;
            AgentThread? agentThread = null!;

            ResponseItem? lastResponseItem = null;
            OpenAIResponse? lastOpenAIResponse = null;

            await foreach (var update in agent.RunStreamingAsync(chatMessages, thread: agentThread, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(update.ResponseId))
                {
                    continue;
                }

                if (sequenceNumber == 1)
                {
                    lastOpenAIResponse = update.AsChatResponse().AsOpenAIResponse();

                    var responseCreated = new StreamingCreatedResponse(sequenceNumber++)
                    {
                        Response = lastOpenAIResponse
                    };
                    yield return GiveNextSseItem(responseCreated);
                }

                if (update.Contents is null || update.Contents.Count == 0)
                {
                    continue;
                }

                // to help convert the AIContent into OpenAI ResponseItem we pack it into the known "chatMessage"
                // and use existing convertion extension method
                var chatMessage = new ChatMessage(ChatRole.Assistant, update.Contents)
                {
                    MessageId = update.MessageId,
                    CreatedAt = update.CreatedAt,
                    RawRepresentation = update.RawRepresentation
                };
                var openAIResponseItem = MicrosoftExtensionsAIResponsesExtensions.AsOpenAIResponseItems([chatMessage]).FirstOrDefault();
                lastResponseItem ??= openAIResponseItem;

                var responseOutputItemAdded = new StreamingOutputItemAddedResponse(sequenceNumber++)
                {
                    OutputIndex = outputIndex++,
                    Item = openAIResponseItem
                };
                yield return GiveNextSseItem(responseOutputItemAdded);
            }

            if (lastResponseItem is not null)
            {
                // we were streaming "response.output_item.added" before
                // so we should complete it now via "response.output_item.done"
                var responseOutputItemAdded = new StreamingOutputItemDoneResponse(sequenceNumber++)
                {
                    OutputIndex = outputIndex++,
                    Item = lastResponseItem
                };

                yield return GiveNextSseItem(responseOutputItemAdded);
            }

            if (lastOpenAIResponse is not null)
            {
                // complete the whole streaming with the full response model
                var responseCompleted = new StreamingCompletedResponse(sequenceNumber++)
                {
                    Response = lastOpenAIResponse
                };
                yield return GiveNextSseItem(responseCompleted);
            }

            static SseItem<StreamingResponseEventBase> GiveNextSseItem<T>(T item) where T : StreamingResponseEventBase
                => new(item, item.Type);
        }
    }
}
