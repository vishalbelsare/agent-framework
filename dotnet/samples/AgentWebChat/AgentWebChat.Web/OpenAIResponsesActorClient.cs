// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents;
using Microsoft.Extensions.AI.Agents.Hosting;
using Microsoft.Extensions.AI.Agents.Runtime;
using OpenAI;
using OpenAI.Responses;

namespace AgentWebChat.Web;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

[SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "for debug / sample purposes")]
[SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value", Justification = "debug")]
internal sealed class OpenAIResponsesActorClient : IActorClient
{
    private readonly string _baseUri;

    public OpenAIResponsesActorClient(string baseUri)
    {
        this._baseUri = baseUri.TrimEnd('/');
    }

    public ValueTask<ActorResponseHandle> GetResponseAsync(ActorId actorId, string messageId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public ValueTask<IActorRuntimeContext?> GetRuntimeContextAsync(ActorId actorId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<ActorResponseHandle> SendRequestAsync(ActorRequest request, CancellationToken cancellationToken)
    {
        // streaming approach
        // return this.SendRequestStreamingAsync(request, cancellationToken);

        // non-streaming approach
        return this.SendRequestNonStreamingAsync(request, cancellationToken);
    }

    private async ValueTask<ActorResponseHandle> SendRequestStreamingAsync(ActorRequest request, CancellationToken cancellationToken)
    {
        // this is a "root" of the OpenAI Responses surface for the agent
        var relativeUri = "/" + request.ActorId.Type + "/v1/";

        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri(this._baseUri + relativeUri)
        };
        OpenAIResponseClient openAiClient = new(
            model: "myModel!",
            credential: new ApiKeyCredential("dummy-key"),
            options: options);

        var chatClient = openAiClient.AsIChatClient();
        var agentRunRequest = request.Params.Deserialize<AgentRunRequest>(AgentHostingJsonUtilities.DefaultOptions);

        // its a sample, so we optimistically expect a message to be here
        Debug.Assert(agentRunRequest is not null);
        Debug.Assert(agentRunRequest.Messages is not null);

        var chatResponseTask = chatClient.GetStreamingResponseAsync(agentRunRequest.Messages, cancellationToken: cancellationToken);
        return new OpenAIActorStreamingResponseHandle(chatResponseTask);
    }

    private async ValueTask<ActorResponseHandle> SendRequestNonStreamingAsync(ActorRequest request, CancellationToken cancellationToken)
    {
        // this is a "root" of the OpenAI Responses surface for the agent
        var relativeUri = "/" + request.ActorId.Type + "/v1/";

        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri(this._baseUri + relativeUri)
        };
        OpenAIResponseClient openAiClient = new(
            model: "myModel!",
            credential: new ApiKeyCredential("dummy-key"),
            options: options);

        var chatClient = openAiClient.AsIChatClient();

        var agentRunRequest = request.Params.Deserialize<AgentRunRequest>(AgentHostingJsonUtilities.DefaultOptions);

        // its a sample, so we optimistically expect a message to be here
        Debug.Assert(agentRunRequest is not null);
        Debug.Assert(agentRunRequest.Messages is not null);

        var chatResponseTask = chatClient.GetResponseAsync(agentRunRequest.Messages, cancellationToken: cancellationToken);
        return new OpenAIActorNonStreamingResponseHandle(chatResponseTask);
    }
}

internal sealed class OpenAIActorStreamingResponseHandle(IAsyncEnumerable<ChatResponseUpdate> chatResponseUpdates) : ActorResponseHandle
{
    public override ValueTask CancelAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<ActorResponse> GetResponseAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override bool TryGetResponse([NotNullWhen(true)] out ActorResponse? response)
    {
        throw new NotImplementedException();
    }

    public override async IAsyncEnumerable<ActorRequestUpdate> WatchUpdatesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chatUpdate in chatResponseUpdates.ConfigureAwait(false))
        {
            var rawJson = JsonSerializer.Serialize(chatUpdate.RawRepresentation);

            // ideally should be done in such a way;
            // but requires Microsoft.Extensions.AI.OpenAI to support all of the response event types the Responses API spec defines
            foreach (var textContent in chatUpdate.Contents.OfType<TextContent>())
            {
                var update = new AgentRunResponseUpdate(ChatRole.Assistant, content: textContent.Text);
                var updateJson = JsonSerializer.SerializeToElement(update);
                yield return new ActorRequestUpdate(RequestStatus.Pending, data: updateJson);
            }
        }

        // complete "streaming"
        yield return new ActorRequestUpdate(RequestStatus.Completed, default);
    }
}

internal sealed class OpenAIActorNonStreamingResponseHandle(Task<ChatResponse> chatResponseTask) : ActorResponseHandle
{
    public override ValueTask CancelAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<ActorResponse> GetResponseAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override bool TryGetResponse([NotNullWhen(true)] out ActorResponse? response)
    {
        throw new NotImplementedException();
    }

    public override async IAsyncEnumerable<ActorRequestUpdate> WatchUpdatesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chatResponse = await chatResponseTask.ConfigureAwait(false);
        Debug.Assert(chatResponse is not null);

        foreach (var message in chatResponse.Messages)
        {
            foreach (var textContent in message.Contents.OfType<TextContent>())
            {
                var update = new AgentRunResponseUpdate(ChatRole.Assistant, content: textContent.Text);
                var updateJson = JsonSerializer.SerializeToElement(update);
                yield return new ActorRequestUpdate(RequestStatus.Pending, data: updateJson);
            }
        }

        // complete "streaming"
        yield return new ActorRequestUpdate(RequestStatus.Completed, default);
    }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
