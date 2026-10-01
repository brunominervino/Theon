using Theon.Metadata;

namespace Theon.Schemas;

// Wraps any schema with a caller-supplied rule that reports for itself.
//
// The ordinary Refine answers yes or no and produces one error, with the Custom code, at the current
// path. That covers most rules and cannot express the rest: a rule with two distinct things to say, a
// rule that belongs against a particular property, a rule that wants a code a caller can branch on.
// The alternative was writing a whole schema against TryParse, which is a lot of ceremony for one
// rule.
//
// The rule runs only once the inner schema has passed, so it never sees a value already known to be
// unacceptable -- the same order the asynchronous refinement uses, and for the same reason.
internal sealed class ContextualRefinedSchema<T>(Schema<T> inner, RefineRule<T> rule) : Schema<T>
{
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Describe(inner).CannotRepresent("Refine");
    }

    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        var errorsBefore = context.ErrorCount;

        if (!inner.TryParse(ref context, input, out output))
        {
            return false;
        }

        rule(output, ref context);
        return context.ErrorCount == errorsBefore;
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        if (!outcome.Succeeded)
        {
            return new ParseOutcome<T>(false, default!);
        }

        // The rule is synchronous, so it gets a synchronous context positioned where this parse has
        // reached. Whatever it records is moved onto the asynchronous context when the loan ends.
        var sync = context.BeginSync();
        try
        {
            rule(outcome.Value, ref sync);
        }
        finally
        {
            context.EndSync(ref sync);
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<T>(true, outcome.Value)
            : new ParseOutcome<T>(false, default!);
    }
}
