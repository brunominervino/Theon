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

    /// <summary>
    /// Gets the deepest nesting the parse will descend into before giving up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recursive schemas make unbounded depth reachable, and a value that refers to itself makes it
    /// infinite. Without a limit that is a stack overflow, which cannot be caught and takes the
    /// process down; with one it is an ordinary validation error.
    /// </para>
    /// <para>
    /// The default of 64 is far beyond any hand-written object graph and far below anything that
    /// threatens the stack.
    /// </para>
    /// </remarks>
    public int MaxDepth { get; init; } = 64;

    /// <summary>Gets the policy that renames a property on its way out.</summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> by default, which is the C# member name and what every release so far
    /// produced. Set it to <see cref="System.Text.Json.JsonNamingPolicy.CamelCase"/>, or to whatever
    /// the application serializes with, and a name a caller sees matches the name they sent.
    /// </para>
    /// <para>
    /// An index is a position and not a name, so a policy does nothing to it.
    /// </para>
    /// </remarks>
    public System.Text.Json.JsonNamingPolicy? PropertyNamingPolicy { get; init; }
}
