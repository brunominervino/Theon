using Theon.Metadata;

namespace Theon.Schemas;

// Carries documentation alongside an inner schema, and validates exactly what the inner one does.
//
// A wrapper rather than an entry in a table keyed on schema instances. Schemas are immutable and
// built once, so this costs one delegating call for a schema that asked for documentation and nothing
// for one that did not, where a side table would mean global mutable state with a lifetime to reason
// about. It also keeps annotating a builder method like every other, returning a new schema.
internal sealed class AnnotatedSchema<TInput, TOutput>(
    Schema<TInput, TOutput> inner,
    string? title,
    string? description,
    object? example,
    bool deprecated) : Schema<TInput, TOutput>
{
    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output) =>
        inner.TryParse(ref context, input, out output);

    public override ValueTask<ParseOutcome<TOutput>> TryParseAsync(
        AsyncParseContext context,
        TInput input) => inner.TryParseAsync(context, input);

    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var described = context.Describe(inner);

        // Written over whatever the inner schema said, because this is the outer and later statement
        // and the caller who wrote it meant it to win. The node is freshly made on every describe,
        // including when the inner schema was promoted to a definition, so nothing shared is touched.
        if (title is not null)
        {
            described.Title = title;
        }

        if (description is not null)
        {
            described.Description = description;
        }

        if (example is not null)
        {
            described.Example = example;
        }

        described.IsDeprecated |= deprecated;

        return described;
    }
}
