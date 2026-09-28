namespace Theon.Errors;

/// <summary>
/// The structured facts about a validation failure, without the human-readable message.
/// </summary>
/// <remarks>
/// This is what a <see cref="SchemaErrorMessageProvider"/> receives. Separating the facts from the
/// message is what makes messages replaceable and localizable: the schema decides <em>what</em>
/// went wrong, the provider decides how to say it.
/// </remarks>
public readonly struct ValidationErrorInfo
{
    /// <summary>Gets the machine-readable kind of failure.</summary>
    public ValidationErrorCode Code { get; init; }

    /// <summary>Gets what kind of quantity a bound was measured against, for bound failures.</summary>
    public ValidationOrigin Origin { get; init; }

    /// <summary>Gets a short description of what was expected, for <see cref="ValidationErrorCode.InvalidType"/>.</summary>
    public string? Expected { get; init; }

    /// <summary>Gets a short description of what was received, when it adds information beyond the value itself.</summary>
    public string? Received { get; init; }

    /// <summary>Gets the declared lower bound, for <see cref="ValidationErrorCode.TooSmall"/>.</summary>
    /// <remarks>
    /// This is always the bound the schema declared, never the measurement that failed it.
    /// Boxed, because this is the cold path and preserving the caller's exact numeric type
    /// matters more here than avoiding one allocation per error.
    /// </remarks>
    public object? Minimum { get; init; }

    /// <summary>Gets the declared upper bound, for <see cref="ValidationErrorCode.TooBig"/>.</summary>
    /// <inheritdoc cref="Minimum" path="/remarks"/>
    public object? Maximum { get; init; }

    /// <summary>Gets a value indicating whether the bound itself is an allowed value.</summary>
    public bool Inclusive { get; init; }

    /// <summary>Gets the name of the format that was not matched, such as <c>email</c> or <c>regex</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Gets the divisor, for <see cref="ValidationErrorCode.NotMultipleOf"/>.</summary>
    public object? Divisor { get; init; }
}
