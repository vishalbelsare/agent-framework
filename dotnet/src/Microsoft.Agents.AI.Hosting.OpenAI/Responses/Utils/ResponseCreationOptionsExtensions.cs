// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Shared.Diagnostics;
using OpenAI.Responses;

namespace Microsoft.Agents.AI.Hosting.OpenAI.Responses.Utils;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "Specifically for accessing hidden members")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Specifically for accessing hidden members")]
internal static class ResponseCreationOptionsExtensions
{
    private static readonly Func<ResponseCreationOptions, bool?> _getStreamNullable;
    private static readonly Func<ResponseCreationOptions, IList<ResponseItem>> _getInput;

    static ResponseCreationOptionsExtensions()
    {
        // OpenAI SDK does not have a simple way to get the input as a c# object.
        // However, it does parse most of the interesting fields into internal properties of `ResponseCreationOptions` object.

        // --- Stream (internal bool? Stream { get; set; }) ---
        const string streamPropName = "Stream";
        var streamProp = typeof(ResponseCreationOptions).GetProperty(streamPropName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(ResponseCreationOptions).FullName!, streamPropName);
        var streamGetter = streamProp.GetGetMethod(nonPublic: true) ?? throw new MissingMethodException($"{streamPropName} getter not found.");

        var param = Expression.Parameter(typeof(ResponseCreationOptions), "o");
        var streamCall = Expression.Call(param, streamGetter);
        _getStreamNullable = Expression.Lambda<Func<ResponseCreationOptions, bool?>>(streamCall, param).Compile();

        // --- Input (internal IList<ResponseItem> Input { get; set; }) ---
        const string inputPropName = "Input";
        var inputProp = typeof(ResponseCreationOptions).GetProperty(inputPropName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(ResponseCreationOptions).FullName!, inputPropName);
        var inputGetter = inputProp.GetGetMethod(nonPublic: true)
            ?? throw new MissingMethodException($"{inputPropName} getter not found.");

        var inputCall = Expression.Call(param, inputGetter);
        _getInput = Expression.Lambda<Func<ResponseCreationOptions, IList<ResponseItem>>>(inputCall, param).Compile();
    }

    public static bool GetStream(this ResponseCreationOptions options)
    {
        Throw.IfNull(options);
        return _getStreamNullable(options) ?? false;
    }

    public static IList<ResponseItem> GetInput(this ResponseCreationOptions options)
    {
        Throw.IfNull(options);
        return _getInput(options);
    }
}
