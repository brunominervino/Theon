namespace Theon.Errors;

/// <summary>
/// Describes what kind of quantity a <see cref="ValidationErrorCode.TooSmall"/> or
/// <see cref="ValidationErrorCode.TooBig"/> bound was measured against.
/// </summary>
/// <remarks>
/// The same error code covers "string too short" and "number too small", which are very different
/// sentences to write in a message. This enum is what lets a message provider tell them apart.
/// </remarks>
public enum ValidationOrigin
{
    /// <summary>No origin applies; the error is not about a bound.</summary>
    None = 0,

    /// <summary>The bound was measured against the length of a string, in Unicode code points.</summary>
    Text,

    /// <summary>The bound was measured against a numeric value.</summary>
    Number,

    /// <summary>The bound was measured against the number of elements in a collection.</summary>
    Collection,

    /// <summary>The bound was measured against a date or time value.</summary>
    DateTime,
}
