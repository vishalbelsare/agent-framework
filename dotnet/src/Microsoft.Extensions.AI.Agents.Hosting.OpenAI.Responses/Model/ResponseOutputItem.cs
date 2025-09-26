// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.Contents;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MessageOutput), "message")]
public abstract class ResponseOutputItem
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

public class MessageOutput : ResponseOutputItem
{
    public override string Type => "message";

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("role")]
    public string Role => "assistant";

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("content")]
    public required IList<MessageContent> Content { get; set; }
}
