// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.Extensions.AI.Agents.Runtime;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses;

/// <summary>
/// OpenAI Responses does not oblige to provide the conversationId to the responseId.
/// This interface provide the API to interact with responseId against the actor model.
/// </summary>
public interface IResponseActorProvider
{
    ValueTask<string> GetActorConversationIdAsync(ActorType actorType, string responseId);

    ValueTask SaveActorConversationIdAsync(ActorId actorId, string responseId);
}
