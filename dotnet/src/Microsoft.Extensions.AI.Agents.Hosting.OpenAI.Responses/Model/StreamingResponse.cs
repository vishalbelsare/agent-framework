// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

public class StreamingResponse
{
    [JsonPropertyName("type")]
    public required StreamingResponseType Type { get; set; }

    [JsonPropertyName("sequence_number")]
    public required int SequenceNumber { get; set; }

    [JsonPropertyName("response")]
    public required Response Response { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
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

internal static class StreamingResponseTypeExtensions
{
    public static string ToEventName(this StreamingResponseType responseType) => responseType switch
    {
        StreamingResponseType.Created => "response.created",
        StreamingResponseType.InProgress => "response.in_progress",
        StreamingResponseType.Completed => "response.completed",
        StreamingResponseType.Failed => "response.failed",
        StreamingResponseType.Incomplete => "response.incomplete",
        _ => throw new ArgumentOutOfRangeException(nameof(responseType), responseType, null)
    };
}
