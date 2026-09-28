namespace Theon.Checks;

/// <summary>
/// One rule applied to a value that has already been established to be of the right type.
/// </summary>
/// <remarks>
/// <para>
/// Checks take the value by reference because some of them rewrite it: <c>Trim</c> is a check, not
/// a separate stage. That keeps ordering honest, since <c>.MinLength(3).Trim()</c> and
/// <c>.Trim().MinLength(3)</c> genuinely mean different things and a reader should be able to see
/// which one they wrote.
/// </para>
/// <para>
/// Checks are objects rather than delegates so that a rule can carry the facts that produced it
/// (the bound, the format name). Those facts reach the error, and later will reach generated
/// OpenAPI documents; a closure would have swallowed them.
/// </para>
/// </remarks>
internal abstract class Check<T>
{
    /// <summary>Gets the message that overrides every provider for failures of this rule.</summary>
    internal string? Message { get; init; }

    /// <summary>
    /// Gets a value indicating whether a failure here should stop the remaining checks on the
    /// same value.
    /// </summary>
    /// <remarks>
    /// False by default, so that independent rules each get to report. Set it where a later rule
    /// would only produce noise given that this one already failed.
    /// </remarks>
    internal virtual bool AbortsOnFailure => false;

    /// <summary>Applies this rule, recording any failure into <paramref name="context"/>.</summary>
    internal abstract void Run(ref ParseContext context, ref T value);
}
