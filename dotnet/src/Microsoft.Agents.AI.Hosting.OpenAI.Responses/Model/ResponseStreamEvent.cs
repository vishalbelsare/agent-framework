// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel.Primitives;
using System.Text.Json.Serialization;
using OpenAI.Responses;

namespace Microsoft.Agents.AI.Hosting.Responses.Model;

#pragma warning disable CA1012 // Abstract types should not have public constructors
public abstract class StreamingResponseEventBase
#pragma warning restore CA1012 // Abstract types should not have public constructors
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("sequence_number")]
    public int SequenceNumber { get; set; }

    [JsonConstructor]
    public StreamingResponseEventBase(string type, int sequenceNumber)
    {
        this.Type = type;
        this.SequenceNumber = sequenceNumber;
    }
}

public class StreamingOutputItemAddedResponse : StreamingResponseEventBase
{
    public const string EventType = "response.output_item.added";

    public StreamingOutputItemAddedResponse(int sequenceNumber) : base(EventType, sequenceNumber)
    {
    }

    [JsonPropertyName("output_index")]
    public int OutputIndex { get; set; }

    [JsonPropertyName("item")]
    public ResponseItem? Item { get; set; }
}

public class StreamingOutputItemDoneResponse : StreamingResponseEventBase
{
    public const string EventType = "response.output_item.done";

    public StreamingOutputItemDoneResponse(int sequenceNumber) : base(EventType, sequenceNumber)
    {
    }

    [JsonPropertyName("output_index")]
    public int OutputIndex { get; set; }

    [JsonPropertyName("item")]
    public ResponseItem? Item { get; set; }
}

public class StreamingCreatedResponse : StreamingResponseEventBase
{
    public const string EventType = "response.created";

    public StreamingCreatedResponse(int sequenceNumber) : base(EventType, sequenceNumber)
    {
    }

    [JsonPropertyName("response")]
    public required OpenAIResponse Response { get; set; }
}

public class StreamingCompletedResponse : StreamingResponseEventBase
{
    public const string EventType = "response.completed";

    public StreamingCompletedResponse(int sequenceNumber) : base(EventType, sequenceNumber)
    {
    }

    [JsonPropertyName("response")]
    public required OpenAIResponse Response { get; set; }
}
