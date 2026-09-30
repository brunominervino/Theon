namespace Theon.Schemas;

// Runs an inner schema and then maps its result to another type.
// The type accepted by the inner schema.
// The type produced by the inner schema.
// The type produced after the mapping.
// The mapping runs only when the inner schema succeeded, so it never sees a value that failed
// validation and never has to defend against one.
internal sealed class TransformSchema<TInput, TIntermediate, TOutput>(
    Schema<TInput, TIntermediate> inner,
    Func<TIntermediate, TOutput> transform) : Schema<TInput, TOutput>
{
    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output)
    {
        if (!inner.TryParse(ref context, input, out var intermediate))
        {
            output = default!;
            return false;
        }

        output = transform(intermediate);
        return true;
    }

    public override async ValueTask<ParseOutcome<TOutput>> TryParseAsync(
        AsyncParseContext context,
        TInput input)
    {
        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        return outcome.Succeeded
            ? new ParseOutcome<TOutput>(true, transform(outcome.Value))
            : new ParseOutcome<TOutput>(false, default!);
    }
}
