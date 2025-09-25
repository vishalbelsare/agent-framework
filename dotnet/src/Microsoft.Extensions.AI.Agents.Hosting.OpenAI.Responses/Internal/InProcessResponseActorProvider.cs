// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.AI.Agents.Runtime;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Internal;

internal class InProcessResponseActorProvider : IResponseActorProvider
{
    private readonly ConcurrentDictionary<ActorTypeResponseIdKey, string> _actorResponseConversationMap = new();

    public ValueTask<string> GetActorConversationIdAsync(ActorType actorType, string responseId)
    {
        var key = new ActorTypeResponseIdKey(actorType, responseId);
        if (this._actorResponseConversationMap.TryGetValue(key, out var conversationId))
        {
            return ValueTask.FromResult(conversationId);
        }

        throw new System.Collections.Generic.KeyNotFoundException($"ResponseId {responseId} not found.");
    }

    public ValueTask SaveActorConversationIdAsync(ActorId actorId, string responseId)
    {
        var key = new ActorTypeResponseIdKey(actorId.Type, responseId);
        this._actorResponseConversationMap.AddOrUpdate(key, actorId.Key, (_, _) => actorId.Key);

        return ValueTask.CompletedTask;
    }

    private struct ActorTypeResponseIdKey : IEquatable<ActorTypeResponseIdKey>
    {
        public ActorType ActorType;
        public string ResponseId;
        public ActorTypeResponseIdKey(ActorType actorType, string responseId)
        {
            this.ActorType = actorType;
            this.ResponseId = responseId;
        }
        public override bool Equals(object? obj)
        {
            if (obj is not ActorTypeResponseIdKey other)
            {
                return false;
            }

            return this.ActorType == other.ActorType && this.ResponseId == other.ResponseId;
        }

        bool IEquatable<ActorTypeResponseIdKey>.Equals(ActorTypeResponseIdKey other)
            => this.ActorType == other.ActorType && this.ResponseId == other.ResponseId;

        public override int GetHashCode() => HashCode.Combine(this.ActorType, this.ResponseId);
    }
}
