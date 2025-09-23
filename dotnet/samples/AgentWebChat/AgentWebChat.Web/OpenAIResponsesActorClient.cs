// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI.Agents.Runtime;
using OpenAI;
using OpenAI.Responses;

namespace AgentWebChat.Web;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class OpenAIResponsesActorClient : IActorClient
{
    private readonly string _baseUri;

    public OpenAIResponsesActorClient(string baseUri)
    {
        this._baseUri = baseUri.TrimEnd('/');
    }

    public ValueTask<ActorResponseHandle> GetResponseAsync(ActorId actorId, string messageId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ActorResponseHandle> SendRequestAsync(ActorRequest request, CancellationToken cancellationToken)
    {
        // non-streaming approach. Ideally for chat we should be doing streaming here.
        return this.SendRequestNonStreaming(request, cancellationToken);
    }

    private async ValueTask<ActorResponseHandle> SendRequestNonStreaming(ActorRequest request, CancellationToken cancellationToken)
    {
        // this is a "root" of the OpenAI Responses surface for the agent
        var relativeUri = "/" + request.ActorId.Type + "/v1/";

        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri(this._baseUri + relativeUri)
        };

        OpenAIResponseClient client = new(
            model: "myModel!",
            credential: new ApiKeyCredential("dummy-key"),
            options: options);

        var data = request.Params.GetRawText();
        var content = BinaryContent.Create(BinaryData.FromString(data));
        var response = await client.CreateResponseAsync(content);

        return new OpenAIActorResponseHandle(response);
    }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal sealed class OpenAIActorResponseHandle(ClientResult result) : ActorResponseHandle
{
    public override ValueTask CancelAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<ActorResponse> GetResponseAsync(CancellationToken cancellationToken)
    {
        result.GetRawResponse();
    }

    public override bool TryGetResponse([NotNullWhen(true)] out ActorResponse? response)
    {
        throw new NotImplementedException();
    }

    public override IAsyncEnumerable<ActorRequestUpdate> WatchUpdatesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
