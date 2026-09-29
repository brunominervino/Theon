namespace Theon.Checks;

// One rule applied to a value that has already been established to be of the right type.
// Checks take the value by reference because some of them rewrite it: Trim is a check, not
// a separate stage. That keeps ordering honest, since .MinLength(3).Trim() and
// .Trim().MinLength(3) genuinely mean different things and a reader should be able to see
// which one they wrote.
// Checks are objects rather than delegates so that a rule can carry the facts that produced it
// (the bound, the format name). Those facts reach the error, and later will reach generated
// OpenAPI documents; a closure would have swallowed them.
internal abstract class Check<T>
{
    // Gets the message that overrides every provider for failures of this rule.
    internal string? Message { get; init; }

    // Gets a value indicating whether a failure here should stop the remaining checks on the
    // same value.
    // False by default, so that independent rules each get to report. Set it where a later rule
    // would only produce noise given that this one already failed.
    internal virtual bool AbortsOnFailure => false;

    // Applies this rule, recording any failure into .
    internal abstract void Run(ref ParseContext context, ref T value);
}
