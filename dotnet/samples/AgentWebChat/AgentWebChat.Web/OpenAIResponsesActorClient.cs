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
        this._baseUri = baseUri;
    }

    public ValueTask<ActorResponseHandle> GetResponseAsync(ActorId actorId, string messageId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async ValueTask<ActorResponseHandle> SendRequestAsync(ActorRequest request, CancellationToken cancellationToken)
    {
        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri(this._baseUri)
        };

        OpenAIResponseClient client = new(
            model: "myModel!",
            credential: new ApiKeyCredential("dummy-key"),
            options: options);

        var data = request.Params.GetRawText();
        var content = BinaryContent.Create(BinaryData.FromString(data));
        var res = await client.CreateResponseAsync(content);

        Console.Write(res);

        return new OpenAIActorResponseHandle();
    }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal sealed class OpenAIActorResponseHandle : ActorResponseHandle
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

    public override IAsyncEnumerable<ActorRequestUpdate> WatchUpdatesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
