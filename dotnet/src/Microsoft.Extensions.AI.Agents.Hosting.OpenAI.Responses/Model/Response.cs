// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

/// <summary>
/// OpenAI Response object
/// </summary>
public class Response
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("background")]
    public required bool Background { get; set; }

    [JsonPropertyName("conversation")]
    public Conversation? Conversation { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("output")]
    public required IList<ResponseOutputItem> Output { get; set; }
}
