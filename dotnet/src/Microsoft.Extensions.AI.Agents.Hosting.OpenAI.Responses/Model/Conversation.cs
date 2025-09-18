// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

internal readonly struct Conversation
{
    private readonly string? _id;
    private readonly ConversationObject? _object;

    public Conversation(string id) => (this._id, this._object) = (id, null);
    public Conversation(ConversationObject obj) => (this._id, this._object) = (null, obj);

    public bool IsId => this._id != null;
    public bool IsObject => this._object != null;

    public string? AsId() => this._id;
    public ConversationObject? AsObject() => this._object;

    public static implicit operator Conversation(string id) => new(id);
    public static implicit operator Conversation(ConversationObject obj) => new(obj);

    public static explicit operator string?(Conversation conversation) => conversation._id;
    public static explicit operator ConversationObject?(Conversation conversation) => conversation._object;
}

internal class ConversationObject
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; } = "conversation";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
