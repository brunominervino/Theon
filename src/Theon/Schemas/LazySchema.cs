using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

// Defers building the inner schema until the first parse, which is what makes a schema able to
// refer to itself: a comment with replies, a category with subcategories, a tree of any shape.
// Without this the declaration would have to name itself before it exists.
//
// The factory runs exactly once, under a lock, and the result is reused. That matters because the
// factory of a recursive schema builds a graph, and building it twice on two threads would produce
// two graphs and waste the work of one.
internal sealed class LazySchema<T>(Func<Schema<T>> factory) : Schema<T>
{
    // Where a cycle is found, because this is the only schema that can contain itself. The context
    // recognises the repeat and writes a reference rather than descending again.
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Describe(_inner.Value);
    }

    private readonly Lazy<Schema<T>> _inner = new(factory, LazyThreadSafetyMode.ExecutionAndPublication);

    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        if (context.IsAtMaxDepth)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.Custom,
                Expected = $"a value nested no deeper than {context.Options.MaxDepth} levels",
            });

            output = default!;
            return false;
        }

        return _inner.Value.TryParse(ref context, input, out output);
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsAtMaxDepth)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.Custom,
                Expected = $"a value nested no deeper than {context.Options.MaxDepth} levels",
            });

            return new ParseOutcome<T>(false, default!);
        }

        return await _inner.Value.TryParseAsync(context, input).ConfigureAwait(false);
    }
}
