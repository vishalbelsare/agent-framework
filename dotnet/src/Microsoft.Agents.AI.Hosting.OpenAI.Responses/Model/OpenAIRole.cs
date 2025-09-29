// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OpenAIRole
{
    User,
    Assistant,
    System,
    Developer
}
