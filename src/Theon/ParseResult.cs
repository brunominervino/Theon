using System.Diagnostics.CodeAnalysis;
using Theon.Errors;

namespace Theon;

/// <summary>
/// The outcome of a parse that does not throw: either a value, or the errors that prevented one.
/// </summary>
/// <typeparam name="T">The type produced by the schema on success.</typeparam>
/// <remarks>
/// A validation failure is an expected outcome of parsing untrusted input, not an exceptional one,
/// so the failing path returns rather than throws. Use <see cref="Schema{TInput, TOutput}.Parse"/>
/// when a failure really is exceptional and should unwind.
/// </remarks>
public readonly struct ParseResult<T>
{
    private readonly IReadOnlyList<ValidationError>? _errors;

    internal ParseResult(T value)
    {
        Value = value;
        _errors = null;
    }

    internal ParseResult(IReadOnlyList<ValidationError> errors)
    {
        Value = default;
        _errors = errors;
    }

    /// <summary>Gets a value indicating whether the value satisfied the schema.</summary>
    public bool IsSuccess => _errors is null or { Count: 0 };

    /// <summary>
    /// Gets the parsed value. Meaningful only when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public T? Value { get; }

    /// <summary>Gets the errors, empty when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<ValidationError> Errors => _errors ?? Array.Empty<ValidationError>();

    /// <summary>Gets the parsed value, or throws if the parse failed.</summary>
    /// <exception cref="SchemaValidationException">The value did not satisfy the schema.</exception>
    public T ValueOrThrow() =>
        IsSuccess ? Value! : throw new SchemaValidationException(Errors);

    /// <summary>Gets the parsed value if the parse succeeded.</summary>
    /// <param name="value">The parsed value, or <see langword="default"/> if the parse failed.</param>
    /// <returns><see langword="true"/> if the parse succeeded.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = Value;
        return IsSuccess;
    }
}
