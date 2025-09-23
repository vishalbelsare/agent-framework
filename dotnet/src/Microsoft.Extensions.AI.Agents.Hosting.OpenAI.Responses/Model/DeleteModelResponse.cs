// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

public class DeleteModelResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("object")]
    public string ObjectType { get; set; } = "response";

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }

    public DeleteModelResponse(bool deleted)
    {
        this.Deleted = deleted;
    }
}
