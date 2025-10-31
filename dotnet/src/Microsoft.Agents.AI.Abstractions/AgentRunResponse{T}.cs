﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.AI;

namespace Microsoft.Agents.AI;

/// <summary>
/// Represents the response of the specified type <typeparamref name="T"/> to an <see cref="AIAgent"/> run request.
/// </summary>
/// <typeparam name="T">The type of value expected from the agent.</typeparam>
public abstract class AgentRunResponse<T> : AgentRunResponse
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunResponse{T}"/> class.</summary>
    protected AgentRunResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunResponse{T}"/> class from an existing <see cref="ChatResponse"/>.
    /// </summary>
    /// <param name="response">The <see cref="ChatResponse"/> from which to populate this <see cref="AgentRunResponse{T}"/>.</param>
    protected AgentRunResponse(ChatResponse response) : base(response)
    {
    }

    /// <summary>
    /// Gets the result value of the agent response as an instance of <typeparamref name="T"/>.
    /// </summary>
    public abstract T Result { get; }
}
