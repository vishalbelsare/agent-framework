﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Hosting.OpenAI.Responses.Converters;
using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Hosting.OpenAI.Responses.Models;

/// <summary>
/// Base class for all item resources (output items from a response).
/// </summary>
[JsonConverter(typeof(ItemResourceConverter))]
internal abstract record ItemResource
{
    /// <summary>
    /// The unique identifier for the item.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The type of the item.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Base class for message item resources.
/// </summary>
[JsonConverter(typeof(ResponsesMessageItemResourceConverter))]
internal abstract record ResponsesMessageItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for message items.
    /// </summary>
    public const string ItemType = "message";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the message.
    /// </summary>
    [JsonPropertyName("status")]
    public ResponsesMessageItemResourceStatus Status { get; init; }

    /// <summary>
    /// The role of the message sender.
    /// </summary>
    [JsonPropertyName("role")]
    public abstract ChatRole Role { get; }
}

/// <summary>
/// An assistant message item resource.
/// </summary>
internal sealed record ResponsesAssistantMessageItemResource : ResponsesMessageItemResource
{
    /// <summary>
    /// The constant role type identifier for assistant messages.
    /// </summary>
    public const string RoleType = "assistant";

    /// <inheritdoc/>
    public override ChatRole Role => ChatRole.Assistant;

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required IList<ItemContent> Content { get; init; }
}

/// <summary>
/// A user message item resource.
/// </summary>
internal sealed record ResponsesUserMessageItemResource : ResponsesMessageItemResource
{
    /// <summary>
    /// The constant role type identifier for user messages.
    /// </summary>
    public const string RoleType = "user";

    /// <inheritdoc/>
    public override ChatRole Role => ChatRole.User;

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required IList<ItemContent> Content { get; init; }
}

/// <summary>
/// A system message item resource.
/// </summary>
internal sealed record ResponsesSystemMessageItemResource : ResponsesMessageItemResource
{
    /// <summary>
    /// The constant role type identifier for system messages.
    /// </summary>
    public const string RoleType = "system";

    /// <inheritdoc/>
    public override ChatRole Role => ChatRole.System;

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required IList<ItemContent> Content { get; init; }
}

/// <summary>
/// A developer message item resource.
/// </summary>
internal sealed record ResponsesDeveloperMessageItemResource : ResponsesMessageItemResource
{
    /// <summary>
    /// The constant role type identifier for developer messages.
    /// </summary>
    public const string RoleType = "developer";

    /// <inheritdoc/>
    public override ChatRole Role => new(RoleType);

    /// <summary>
    /// The content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required IList<ItemContent> Content { get; init; }
}

/// <summary>
/// A function tool call item resource.
/// </summary>
internal sealed record FunctionToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for function call items.
    /// </summary>
    public const string ItemType = "function_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the function call.
    /// </summary>
    [JsonPropertyName("status")]
    public FunctionToolCallItemResourceStatus Status { get; init; }

    /// <summary>
    /// The call ID of the function.
    /// </summary>
    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }

    /// <summary>
    /// The name of the function.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The arguments to the function.
    /// </summary>
    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }
}

/// <summary>
/// A function tool call output item resource.
/// </summary>
internal sealed record FunctionToolCallOutputItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for function call output items.
    /// </summary>
    public const string ItemType = "function_call_output";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the function call output.
    /// </summary>
    [JsonPropertyName("status")]
    public FunctionToolCallOutputItemResourceStatus Status { get; init; }

    /// <summary>
    /// The call ID of the function.
    /// </summary>
    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }

    /// <summary>
    /// The output of the function.
    /// </summary>
    [JsonPropertyName("output")]
    public required string Output { get; init; }
}

/// <summary>
/// The status of a message item resource.
/// </summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<ResponsesMessageItemResourceStatus>))]
public enum ResponsesMessageItemResourceStatus
{
    /// <summary>
    /// The message is completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The message is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The message is incomplete.
    /// </summary>
    Incomplete
}

/// <summary>
/// The status of a function tool call item resource.
/// </summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<FunctionToolCallItemResourceStatus>))]
public enum FunctionToolCallItemResourceStatus
{
    /// <summary>
    /// The function call is completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The function call is in progress.
    /// </summary>
    InProgress
}

/// <summary>
/// The status of a function tool call output item resource.
/// </summary>
[JsonConverter(typeof(SnakeCaseEnumConverter<FunctionToolCallOutputItemResourceStatus>))]
public enum FunctionToolCallOutputItemResourceStatus
{
    /// <summary>
    /// The function call output is completed.
    /// </summary>
    Completed
}

/// <summary>
/// Base class for item content.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ItemContentInputText), "input_text")]
[JsonDerivedType(typeof(ItemContentInputAudio), "input_audio")]
[JsonDerivedType(typeof(ItemContentInputImage), "input_image")]
[JsonDerivedType(typeof(ItemContentInputFile), "input_file")]
[JsonDerivedType(typeof(ItemContentOutputText), "output_text")]
[JsonDerivedType(typeof(ItemContentOutputAudio), "output_audio")]
[JsonDerivedType(typeof(ItemContentRefusal), "refusal")]
internal abstract record ItemContent
{
    /// <summary>
    /// The type of the content.
    /// </summary>
    [JsonIgnore]
    public abstract string Type { get; }

    /// <summary>
    /// Gets or sets the original representation of the content, if applicable.
    /// This property is not serialized and is used for round-tripping conversions.
    /// </summary>
    [JsonIgnore]
    public object? RawRepresentation { get; set; }
}

/// <summary>
/// Text input content.
/// </summary>
internal sealed record ItemContentInputText : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "input_text";

    /// <summary>
    /// The text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Audio input content.
/// </summary>
internal sealed record ItemContentInputAudio : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "input_audio";

    /// <summary>
    /// Base64-encoded audio data.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// The format of the audio data.
    /// </summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }
}

/// <summary>
/// Image input content.
/// </summary>
internal sealed record ItemContentInputImage : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "input_image";

    /// <summary>
    /// The URL of the image to be sent to the model. A fully qualified URL or base64 encoded image in a data URL.
    /// </summary>
    [JsonPropertyName("image_url")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "OpenAI API uses string for image_url")]
    public string? ImageUrl { get; init; }

    /// <summary>
    /// The ID of the file to be sent to the model.
    /// </summary>
    [JsonPropertyName("file_id")]
    public string? FileId { get; init; }

    /// <summary>
    /// The detail level of the image to be sent to the model. One of 'high', 'low', or 'auto'. Defaults to 'auto'.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

/// <summary>
/// File input content.
/// </summary>
internal sealed record ItemContentInputFile : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "input_file";

    /// <summary>
    /// The ID of the file to be sent to the model.
    /// </summary>
    [JsonPropertyName("file_id")]
    public string? FileId { get; init; }

    /// <summary>
    /// The name of the file to be sent to the model.
    /// </summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    /// <summary>
    /// The content of the file to be sent to the model.
    /// </summary>
    [JsonPropertyName("file_data")]
    public string? FileData { get; init; }
}

/// <summary>
/// Text output content.
/// </summary>
internal sealed record ItemContentOutputText : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "output_text";

    /// <summary>
    /// The text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>
    /// The annotations.
    /// </summary>
    [JsonPropertyName("annotations")]
    public required IList<JsonElement> Annotations { get; init; }

    /// <summary>
    /// Log probability information for the output tokens.
    /// </summary>
    [JsonPropertyName("logprobs")]
    public IList<JsonElement> Logprobs { get; init; } = [];
}

/// <summary>
/// Audio output content.
/// </summary>
internal sealed record ItemContentOutputAudio : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "output_audio";

    /// <summary>
    /// Base64-encoded audio data from the model.
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// The transcript of the audio data from the model.
    /// </summary>
    [JsonPropertyName("transcript")]
    public required string Transcript { get; init; }
}

/// <summary>
/// Refusal content.
/// </summary>
internal sealed record ItemContentRefusal : ItemContent
{
    /// <inheritdoc/>
    [JsonIgnore]
    public override string Type => "refusal";

    /// <summary>
    /// The refusal explanation from the model.
    /// </summary>
    [JsonPropertyName("refusal")]
    public required string Refusal { get; init; }
}

// Additional ItemResource types from TypeSpec

/// <summary>
/// A file search tool call item resource.
/// </summary>
internal sealed record FileSearchToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for file search call items.
    /// </summary>
    public const string ItemType = "file_search_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the file search.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A computer tool call item resource.
/// </summary>
internal sealed record ComputerToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for computer call items.
    /// </summary>
    public const string ItemType = "computer_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the computer call.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A computer tool call output item resource.
/// </summary>
internal sealed record ComputerToolCallOutputItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for computer call output items.
    /// </summary>
    public const string ItemType = "computer_call_output";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the computer call output.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A web search tool call item resource.
/// </summary>
internal sealed record WebSearchToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for web search call items.
    /// </summary>
    public const string ItemType = "web_search_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the web search.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A reasoning item resource.
/// </summary>
internal sealed record ReasoningItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for reasoning items.
    /// </summary>
    public const string ItemType = "reasoning";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the reasoning.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// An item reference item resource.
/// </summary>
internal sealed record ItemReferenceItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for item reference items.
    /// </summary>
    public const string ItemType = "item_reference";

    /// <inheritdoc/>
    public override string Type => ItemType;
}

/// <summary>
/// An image generation tool call item resource.
/// </summary>
internal sealed record ImageGenerationToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for image generation call items.
    /// </summary>
    public const string ItemType = "image_generation_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the image generation.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A code interpreter tool call item resource.
/// </summary>
internal sealed record CodeInterpreterToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for code interpreter call items.
    /// </summary>
    public const string ItemType = "code_interpreter_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the code interpreter.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A local shell tool call item resource.
/// </summary>
internal sealed record LocalShellToolCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for local shell call items.
    /// </summary>
    public const string ItemType = "local_shell_call";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the local shell call.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// A local shell tool call output item resource.
/// </summary>
internal sealed record LocalShellToolCallOutputItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for local shell call output items.
    /// </summary>
    public const string ItemType = "local_shell_call_output";

    /// <inheritdoc/>
    public override string Type => ItemType;

    /// <summary>
    /// The status of the local shell call output.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

/// <summary>
/// An MCP list tools item resource.
/// </summary>
internal sealed record MCPListToolsItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for MCP list tools items.
    /// </summary>
    public const string ItemType = "mcp_list_tools";

    /// <inheritdoc/>
    public override string Type => ItemType;
}

/// <summary>
/// An MCP approval request item resource.
/// </summary>
internal sealed record MCPApprovalRequestItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for MCP approval request items.
    /// </summary>
    public const string ItemType = "mcp_approval_request";

    /// <inheritdoc/>
    public override string Type => ItemType;
}

/// <summary>
/// An MCP approval response item resource.
/// </summary>
internal sealed record MCPApprovalResponseItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for MCP approval response items.
    /// </summary>
    public const string ItemType = "mcp_approval_response";

    /// <inheritdoc/>
    public override string Type => ItemType;
}

/// <summary>
/// An MCP call item resource.
/// </summary>
internal sealed record MCPCallItemResource : ItemResource
{
    /// <summary>
    /// The constant item type identifier for MCP call items.
    /// </summary>
    public const string ItemType = "mcp_call";

    /// <inheritdoc/>
    public override string Type => ItemType;
}
