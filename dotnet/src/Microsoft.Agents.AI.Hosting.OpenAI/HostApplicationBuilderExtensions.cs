﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for <see cref="IHostApplicationBuilder"/> to configure OpenAI Responses support.
/// </summary>
public static class MicrosoftAgentAIHostingOpenAIHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds support for exposing <see cref="AIAgent"/> instances via OpenAI Responses.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> for method chaining.</returns>
    public static IHostApplicationBuilder AddOpenAIResponses(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOpenAIResponses();

        return builder;
    }
}
