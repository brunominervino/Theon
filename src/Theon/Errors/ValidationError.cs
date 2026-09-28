using System.Diagnostics;

namespace Theon.Errors;

/// <summary>
/// A single validation failure: where it happened, what kind of failure it was, and what to tell
/// the user about it.
/// </summary>
[DebuggerDisplay("{Path,nq}: {Message,nq} ({Code})")]
public sealed class ValidationError
{
    private readonly ValidationErrorInfo _info;

    internal ValidationError(ValidationPath path, string message, in ValidationErrorInfo info)
    {
        Path = path;
        Message = message;
        _info = info;
    }

    /// <summary>Gets the location of the failure within the parsed value.</summary>
    public ValidationPath Path { get; }

    /// <summary>Gets the resolved, human-readable message.</summary>
    public string Message { get; }

    /// <summary>Gets the structured facts about this failure.</summary>
    public ref readonly ValidationErrorInfo Info => ref _info;

    /// <summary>Gets the machine-readable kind of failure.</summary>
    public ValidationErrorCode Code => _info.Code;

    /// <summary>Gets what kind of quantity a bound was measured against, for bound failures.</summary>
    public ValidationOrigin Origin => _info.Origin;

    /// <summary>Gets the name of the format that was not matched, if any.</summary>
    public string? Format => _info.Format;

    /// <summary>Returns the path and message, for example <c>address.zipCode: Invalid postcode</c>.</summary>
    public override string ToString() =>
        Path.IsRoot ? Message : string.Concat(Path.ToString(), ": ", Message);
}
