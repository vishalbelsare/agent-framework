// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using OpenAI.Responses;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

internal class CreateResponse
{
    [JsonPropertyName("conversation")]
    public Conversation Conversation { get; set; }

    [JsonPropertyName("input")]
    public IList<ResponseItem>? Input { get; set; }

    [JsonPropertyName("include")]
    public IList<IncludeParameter>? Include { get; set; }
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
