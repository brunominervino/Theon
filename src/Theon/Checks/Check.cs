using Theon.Metadata;

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

    // Contributes this rule to a description of the schema that holds it.
    //
    // The default contributes no constraint, which is the right answer for a rule a document cannot
    // express: a refinement is an arbitrary predicate with no keyword to map to, and a normalization
    // rewrites the value rather than constraining it. Leaving such a rule out makes the document
    // incomplete; inventing a keyword for it would make the document wrong.
    //
    // What it does contribute is the fact that it could not be expressed, so that a caller who asked
    // to be told about that can be. Every rule that has nothing to say is reported by not overriding
    // this, which means a rule added later is covered without anyone remembering to cover it.
    internal virtual void Describe(SchemaDescriptionBuilder description)
    {
        ArgumentNullException.ThrowIfNull(description);
        description.CannotRepresent(RuleName);
    }

    // The type name, without the generic arity a runtime type name carries. Read at describe time
    // only, which is a start-up or tooling operation and never a parse.
    private string RuleName
    {
        get
        {
            var name = GetType().Name;
            var arity = name.IndexOf('`', StringComparison.Ordinal);
            return arity < 0 ? name : name[..arity];
        }
    }
}
