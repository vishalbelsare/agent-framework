// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents.Runtime;
using OpenAI;
using OpenAI.Responses;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI.Agents;

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
        return this.SendRequestNonStreamingAsync(request, cancellationToken);
    }

    private async ValueTask<ActorResponseHandle> SendRequestNonStreamingAsync(ActorRequest request, CancellationToken cancellationToken)
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
        var getResponseTask = client.CreateResponseAsync(content);

        return new OpenAIActorResponseHandle(request, getResponseTask);
    }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal sealed class OpenAIActorResponseHandle(
    ActorRequest request,
    Task<ClientResult> responseTask) : ActorResponseHandle
{
    public override ValueTask CancelAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask<ActorResponse> GetResponseAsync(CancellationToken cancellationToken)
    {
        var data = await this.FetchDataAsync(cancellationToken);

        return new ActorResponse
        {
            ActorId = request.ActorId,
            MessageId = Guid.NewGuid().ToString(), // this should be the response, can be fetched from data if needed.
            Status = RequestStatus.Completed,
            Data = data
        };
    }

    public override bool TryGetResponse([NotNullWhen(true)] out ActorResponse? response)
    {
        throw new NotImplementedException();
    }

    public override async IAsyncEnumerable<ActorRequestUpdate> WatchUpdatesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var data = await this.FetchDataAsync(cancellationToken);
        var openAIContent = GetOpenAIContent(data);

        var update = new AgentRunResponseUpdate(ChatRole.Assistant, content: openAIContent);
        var updateJson = JsonSerializer.SerializeToElement(update);

        yield return new ActorRequestUpdate(RequestStatus.Pending, data: updateJson);

        // complete "streaming"
        yield return new ActorRequestUpdate(RequestStatus.Completed, default);
    }

    private async ValueTask<JsonElement> FetchDataAsync(CancellationToken cancellationToken)
    {
        var clientResult = await responseTask;
        var rawResponse = clientResult.GetRawResponse();

        var binaryData = rawResponse.Content;
        var jsonString = binaryData.ToString();
        return JsonSerializer.Deserialize<JsonElement>(jsonString);
    }

    private static string GetOpenAIContent(JsonElement data)
    {
        try
        {
            // this is a direct attempt to parse the text from openai response.
            // It will later be packed in agent-framework output types.

            var output = data.GetProperty("output");
            var content = output[0].GetProperty("content");
            var text = content[0].GetProperty("text");
            return text.GetString()!;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid response schema: ", ex);
        }
    }
}
