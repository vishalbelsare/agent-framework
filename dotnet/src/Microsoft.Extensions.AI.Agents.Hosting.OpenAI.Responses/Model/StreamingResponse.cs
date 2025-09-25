// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

public class StreamingResponse
{
    [JsonPropertyName("type")]
    public required StreamingResponseType Type { get; set; }

    [JsonPropertyName("response")]
    public required Response Response { get; set; }
}

public enum StreamingResponseType
{
    [JsonStringEnumMemberName("response.created")]
    Created,

    [JsonStringEnumMemberName("response.in_progress")]
    InProgress,

    [JsonStringEnumMemberName("response.completed")]
    Completed,

    [JsonStringEnumMemberName("response.failed")]
    Failed,

    [JsonStringEnumMemberName("response.incomplete")]
    Incomplete
}
