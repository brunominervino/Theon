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

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var described = context.Describe(inner);

        // Each of these wins over whatever the inner schema said, because this is the outer and later
        // statement and the caller who wrote it meant it to win. Deprecation only accumulates:
        // annotating something as not deprecated is not a thing anyone does.
        return new SchemaDescription(described)
        {
            Title = title ?? described.Title,
            Description = description ?? described.Description,
            Example = example ?? described.Example,
            IsDeprecated = described.IsDeprecated || deprecated,
        };
    }
}
