using Theon.Errors;

namespace Theon;

/// <summary>
/// Per-call options for <see cref="Schema{TInput, TOutput}.Parse"/> and
/// <see cref="Schema{TInput, TOutput}.SafeParse"/>.
/// </summary>
public sealed class ParseOptions
{
    /// <summary>Gets the options used when none are supplied.</summary>
    public static ParseOptions Default { get; } = new();

    /// <summary>
    /// Gets a provider consulted for every error message raised during this call, before the
    /// global provider and the built-in defaults.
    /// </summary>
    public SchemaErrorMessageProvider? MessageProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether parsing stops as soon as the first error is produced.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>, which collects every error so a form can be
    /// corrected in one pass. Set it to <see langword="true"/> when only the yes-or-no answer
    /// matters, which lets large objects stop early.
    /// </remarks>
    public bool StopOnFirstError { get; init; }
}
