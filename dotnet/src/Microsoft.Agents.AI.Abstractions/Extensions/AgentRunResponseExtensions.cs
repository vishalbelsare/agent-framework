// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI.Extensions;

/// <summary>
/// Provides extension methods for <see cref="AgentRunResponse"/>.
/// </summary>
public static class AgentRunResponseExtensions
{
    /// <summary>
    /// Converts an <see cref="AgentRunResponse"/> instance to a <see cref="ChatResponse"/>.
    /// </summary>
    /// <param name="response">The <see cref="AgentRunResponse"/> to convert. Cannot be null.</param>
    /// <returns>A <see cref="ChatResponse"/> populated with values from <paramref name="response"/>.</returns>
    public static ChatResponse AsChatResponse(this AgentRunResponse response) => new(response.Messages)
    {
        CreatedAt = response.CreatedAt,
        ResponseId = response.ResponseId,
        RawRepresentation = response.RawRepresentation,
        AdditionalProperties = response.AdditionalProperties,
        Usage = response.Usage,
    };
}
