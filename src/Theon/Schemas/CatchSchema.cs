using Theon.Metadata;

namespace Theon.Schemas;

// Runs an inner schema and, if it rejects the value, reports nothing and hands back a fallback.
//
// The inner schema runs on a forked context, so the errors it raised are dropped rather than
// filtered out afterwards. Filtering would mean knowing which of the errors already on the context
// belonged to this subtree, and a schema has no business asking that question.
//
// A validation failure is swallowed; an exception is not. A predicate that threw, or a schema with
// an asynchronous rule parsed synchronously, is a defect in the program rather than a value a person
// typed wrongly, and hiding it behind a fallback would turn a loud failure at the first call into a
// wrong answer for ever.
internal sealed class CatchSchema<TInput, TOutput>(Schema<TInput, TOutput> inner, TOutput fallback)
    : Schema<TInput, TOutput>
{
    // Describes the inner shape, and records the fallback as the default.
    //
    // This is the one description that is stricter than the schema: a caught schema accepts anything,
    // so a document faithful to that would say "anything" and tell a reader nothing about what to
    // send. The intended shape plus the value they get when they miss it is the more useful pair, and
    // "default" is an annotation rather than an assertion, so nothing here claims to be enforced.
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var described = context.Describe(inner);
        described.DefaultValue = fallback;
        described.HasDefaultValue = true;
        return described;
    }

    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output)
    {
        var attempt = context.Fork();

        if (inner.TryParse(ref attempt, input, out var parsed) && !attempt.HasErrors)
        {
            output = parsed;
            return true;
        }

        output = fallback;
        return true;
    }

    public override async ValueTask<ParseOutcome<TOutput>> TryParseAsync(
        AsyncParseContext context,
        TInput input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attempt = context.Fork();
        var outcome = await inner.TryParseAsync(attempt, input).ConfigureAwait(false);

        return outcome.Succeeded && !attempt.HasErrors
            ? outcome
            : new ParseOutcome<TOutput>(true, fallback);
    }
}
