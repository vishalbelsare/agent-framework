// Copyright (c) Microsoft. All rights reserved.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.AI.Agents.Hosting.Responses.Model;

public readonly struct IncludeParameter : IEquatable<IncludeParameter>, IParsable<IncludeParameter>
{
    private readonly string _value;
    private const string FileSearchCallResultsValue = "file_search_call.results";
    private const string MessageInputImageImageUrlValue = "message.input_image.image_url";
    private const string ComputerCallOutputOutputImageUrlValue = "computer_call_output.output.image_url";
    private const string ReasoningEncryptedContentValue = "reasoning.encrypted_content";
    private const string CodeInterpreterCallOutputsValue = "code_interpreter_call.outputs";

    public IncludeParameter(string value)
    {
        this._value = value;
    }

    internal static IncludeParameter FileSearchCallResults { get; } = new IncludeParameter(FileSearchCallResultsValue);
    internal static IncludeParameter MessageInputImageImageUrl { get; } = new IncludeParameter(MessageInputImageImageUrlValue);
    internal static IncludeParameter ComputerCallOutputOutputImageUrl { get; } = new IncludeParameter(ComputerCallOutputOutputImageUrlValue);
    internal static IncludeParameter ReasoningEncryptedContent { get; } = new IncludeParameter(ReasoningEncryptedContentValue);
    internal static IncludeParameter CodeInterpreterCallOutputs { get; } = new IncludeParameter(CodeInterpreterCallOutputsValue);

    public static bool operator ==(IncludeParameter left, IncludeParameter right) => left.Equals(right);
    public static bool operator !=(IncludeParameter left, IncludeParameter right) => !left.Equals(right);

    public static implicit operator IncludeParameter(string value) => new(value);
    public static implicit operator IncludeParameter?(string value) => value != null ? new IncludeParameter(value) : default /* TODO verify */;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals(object? obj) => obj is IncludeParameter other && this.Equals(other);
    public bool Equals(IncludeParameter other) => string.Equals(this._value, other._value, StringComparison.OrdinalIgnoreCase);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode() => this._value != null ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(this._value) : 0;

    public override string ToString() => this._value;

    public static IncludeParameter Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out IncludeParameter result)
    {
        if (!string.IsNullOrEmpty(s))
        {
            result = new IncludeParameter(s);
            return true;
        }

        result = default;
        return false;
    }
}
