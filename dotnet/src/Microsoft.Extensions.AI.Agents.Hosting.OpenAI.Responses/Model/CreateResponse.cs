// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class CreateResponse
{
    [JsonPropertyName("conversation")]
    public Conversation? Conversation { get; set; }

    [JsonPropertyName("input")]
    public IList<ResponseInputItem>? Input { get; set; }

    [JsonPropertyName("include")]
    public IList<IncludeParameter>? Include { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
