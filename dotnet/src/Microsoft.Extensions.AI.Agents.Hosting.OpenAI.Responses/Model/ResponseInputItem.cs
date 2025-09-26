// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model.Contents;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

/// <summary>
/// Represents input that can be either a simple text string or a list of input items.
/// </summary>
[JsonConverter(typeof(ResponseInputItemConverter))]
public class ResponseInputItem
{
    /// <summary>
    /// Gets or sets the text input when the input is a simple string.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the list of input items when the input is an array.
    /// </summary>
    public IList<ResponseInputMessage>? Items { get; set; }

    /// <summary>
    /// Gets a value indicating whether this input is a simple text string.
    /// </summary>
    public bool IsText => this.Text != null && this.Items == null;

    /// <summary>
    /// Gets a value indicating whether this input is a list of items.
    /// </summary>
    public bool IsItemList => this.Items != null && this.Text == null;

    /// <summary>
    /// Creates a ResponseInput from a text string.
    /// </summary>
    public static implicit operator ResponseInputItem(string text) => new() { Text = text };

    /// <summary>
    /// Creates a ResponseInput from a list of items.
    /// </summary>
    public static implicit operator ResponseInputItem(List<ResponseInputMessage> items) => new() { Items = items };
}

/// <summary>
/// A complex object definition of an input item to the model.
/// </summary>
public class ResponseInputMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("role")]
    public required OpenAIRole Role { get; set; }

    [JsonPropertyName("content")]
    public required IList<MessageContent> Content { get; set; }
}

/// <summary>
/// JSON converter for ResponseInput that handles both string and array formats.
/// </summary>
public sealed class ResponseInputItemConverter : JsonConverter<ResponseInputItem?>
{
    public override ResponseInputItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType is JsonTokenType.String)
        {
            return new ResponseInputItem { Text = reader.GetString() };
        }

        if (reader.TokenType is JsonTokenType.StartArray)
        {
            var items = JsonSerializer.Deserialize<List<ResponseInputMessage>>(ref reader, options);
            return new ResponseInputItem { Items = items };
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for ResponseInput");
    }

    public override void Write(Utf8JsonWriter writer, ResponseInputItem? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value.IsText)
        {
            writer.WriteStringValue(value.Text);
        }
        else if (value.IsItemList)
        {
            JsonSerializer.Serialize(writer, value.Items, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
