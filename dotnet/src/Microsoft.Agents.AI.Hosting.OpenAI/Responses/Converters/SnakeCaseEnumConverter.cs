﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.AI.Hosting.OpenAI.Responses.Converters;

/// <summary>
/// JSON converter for enums that uses snake_case naming convention.
/// </summary>
/// <typeparam name="T">The enum type to convert.</typeparam>
[ExcludeFromCodeCoverage]
internal sealed class SnakeCaseEnumConverter<T> : JsonStringEnumConverter<T> where T : struct, Enum
{
    public SnakeCaseEnumConverter() : base(JsonNamingPolicy.SnakeCaseLower)
    {
    }
}
