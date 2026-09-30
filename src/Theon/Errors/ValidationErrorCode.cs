namespace Theon.Errors;

/// <summary>
/// Identifies the kind of a <see cref="ValidationError"/> in a stable, machine-readable way.
/// </summary>
/// <remarks>
/// Codes are the contract; messages are not. A caller that branches on validation outcomes
/// should switch on this enum rather than matching message text, because messages are
/// localizable and may be replaced by the application.
/// </remarks>
public enum ValidationErrorCode
{
    /// <summary>The value was of the wrong type, or was <see langword="null"/> where a value was required.</summary>
    InvalidType = 1,

    /// <summary>The value was below a minimum bound, shorter than a minimum length, or had too few elements.</summary>
    TooSmall,

    /// <summary>The value was above a maximum bound, longer than a maximum length, or had too many elements.</summary>
    TooBig,

    /// <summary>The value did not match a required textual format, such as an e-mail address or a regular expression.</summary>
    InvalidFormat,

    /// <summary>The value was not an exact multiple of a required divisor.</summary>
    NotMultipleOf,

    /// <summary>The value was not one of an allowed set of values.</summary>
    InvalidValue,

    /// <summary>A user-supplied refinement rejected the value.</summary>
    Custom,

    /// <summary>The value was not the single value a literal rule requires.</summary>
    /// <remarks>
    /// Distinct from <see cref="InvalidValue"/>, which says the value was not among several that
    /// would have been accepted. This one says exactly which value was wanted, and it is the code a
    /// caller matches to find the branch of a discriminated union that did not line up.
    /// </remarks>
    NotEqual,

    /// <summary>The collection contained more than one equal element where each had to be distinct.</summary>
    /// <remarks>
    /// Reported at the index of the repeat, not at the collection, so the path names the element a
    /// person has to change rather than the field that contains it.
    /// </remarks>
    Duplicate,
}
