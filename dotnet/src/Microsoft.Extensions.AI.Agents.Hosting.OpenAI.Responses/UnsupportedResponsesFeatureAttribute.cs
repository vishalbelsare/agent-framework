// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses;

/// <summary>
/// Indicates that an OpenAI Responses feature is not supported by the agent framework.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class UnsupportedResponsesFeatureAttribute : Attribute
{
    /// <summary>
    /// Gets the reason why this member is unsupported.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedResponsesFeatureAttribute"/> class with a reason.
    /// </summary>
    /// <param name="reason">The reason why this member is unsupported.</param>
    public UnsupportedResponsesFeatureAttribute(string reason)
    {
        this.Reason = reason;
    }
}
