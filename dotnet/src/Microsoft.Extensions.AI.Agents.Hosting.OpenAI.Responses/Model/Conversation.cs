// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

[JsonConverter(typeof(ConversationConverter))]
public class Conversation
{
    public string? Id { get; set; }
    public ConversationObject? RawObject { get; set; }

    public string? ConversationId => this.Id ?? this.RawObject?.Id;
    public bool IsId => this.Id != null;
    public bool IsObject => this.RawObject != null;
}

public class ConversationObject
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; set; }

    [JsonPropertyName("object")]
    public string RawObject { get; set; } = "conversation";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
internal class ConversationConverter : JsonConverter<Conversation>
{
    public override Conversation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new Conversation { Id = reader.GetString() };
        }

        return new Conversation { RawObject = JsonSerializer.Deserialize<ConversationObject>(ref reader, options) };
    }

    public override void Write(Utf8JsonWriter writer, Conversation value, JsonSerializerOptions options)
    {
        if (value.IsId)
        {
            writer.WriteStringValue(value.Id);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.RawObject, options);
        }
    }
}
