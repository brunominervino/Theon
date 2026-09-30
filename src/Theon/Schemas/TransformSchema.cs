using Theon.Metadata;

namespace Theon.Schemas;

// Runs an inner schema and then maps its result to another type.
// The type accepted by the inner schema.
// The type produced by the inner schema.
// The type produced after the mapping.
// The mapping runs only when the inner schema succeeded, so it never sees a value that failed
// validation and never has to defend against one.
internal sealed class TransformSchema<TInput, TIntermediate, TOutput>(
    Schema<TInput, TIntermediate> inner,
    Func<TIntermediate, TOutput> transform,
    Schema<TOutput>? then) : Schema<TInput, TOutput>
{
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A document describes what a caller sends, which is the input side of the chain. What the
        // transform turns it into is this program's business and not the caller's.
        var described = context.Describe(inner);

        // Rules applied after the transform constrain the converted value, and the dialect has no way
        // to say "the number this text parses to is between one and a hundred". Recorded rather than
        // dropped, so that a caller who asked to be told can be.
        if (then is not null)
        {
            described.CannotRepresent("rules applied after Transform");
        }

        return described;
    }

    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output)
    {
        if (!inner.TryParse(ref context, input, out var intermediate))
        {
            output = default!;
            return false;
        }

        var transformed = transform(intermediate);

        if (then is null)
        {
            output = transformed;
            return true;
        }

        var succeeded = then.TryParse(ref context, transformed, out var validated);
        output = succeeded ? validated : default!;
        return succeeded;
    }

    public override async ValueTask<ParseOutcome<TOutput>> TryParseAsync(
        AsyncParseContext context,
        TInput input)
    {
        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        if (!outcome.Succeeded)
        {
            return new ParseOutcome<TOutput>(false, default!);
        }

        var transformed = transform(outcome.Value);

        return then is null
            ? new ParseOutcome<TOutput>(true, transformed)
            : await then.TryParseAsync(context, transformed).ConfigureAwait(false);
    }
}
