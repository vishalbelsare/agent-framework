// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class CreateResponse
{
    [JsonPropertyName("background")]
    public bool Background { get; set; }

    [JsonPropertyName("conversation")]
    public Conversation? Conversation { get; set; }

    [JsonPropertyName("input")]
    public ResponseInputItem? Input { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("instructions")]
    [UnsupportedResponsesFeature("'instructions' are already supplied to the AI Agent.")]
    public string? Instructions { get; set; }

    [JsonPropertyName("model")]
    [UnsupportedResponsesFeature("'model' is already connected to the AI Agent.")]
    public string? Model { get; set; }

    [JsonPropertyName("metadata")]
    public IDictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("store")]
    public bool Store { get; set; } = true;
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
