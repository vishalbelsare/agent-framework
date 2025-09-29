// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

namespace Microsoft.Agents.AI.Hosting.Responses.Mapping;

internal static class OpenAIRoleMapper
{
    public static OpenAIRole ToOpenAIRole(this ChatRole chatRole) => chatRole.Value switch
    {
        "system" => OpenAIRole.System,
        "assistant" => OpenAIRole.Assistant,
        "user" => OpenAIRole.User,
        "developer" => OpenAIRole.Developer,
        _ => throw new ArgumentOutOfRangeException(nameof(chatRole), chatRole, "Unknown")
    };

    public static ChatRole ToChatRole(this OpenAIRole openAIRole) => openAIRole switch
    {
        OpenAIRole.User => ChatRole.User,
        OpenAIRole.Assistant => ChatRole.Assistant,
        OpenAIRole.System => ChatRole.System,
        OpenAIRole.Developer => new ChatRole("developer"),
        _ => throw new ArgumentOutOfRangeException(nameof(openAIRole), openAIRole, "Unknown OpenAI role"),
    };
}
