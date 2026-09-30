using Theon.Metadata;

namespace Theon.Schemas;

// Requires two schemas of the same type to be satisfied at once.
//
// Both see the same input, and both report. That is what makes this an intersection rather than a
// pipeline: the two schemas were written independently — one by the platform, one by the tenant —
// and neither is the input to the other. Feeding the left one's output to the right one would be
// Transform, which already exists and reads as what it is.
//
// The output is the left schema's. Only one of the two can be kept, and the left is the one a reader
// scanning the chain sees first. A schema that normalizes therefore belongs on the left.
internal sealed class IntersectionSchema<T>(Schema<T> left, Schema<T> right) : Schema<T>
{
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new SchemaDescription
        {
            AllOf = [context.Describe(left), context.Describe(right)],
        };
    }

    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        var errorsBefore = context.ErrorCount;

        left.TryParse(ref context, input, out output);

        if (!context.ShouldStop)
        {
            right.TryParse(ref context, input, out _);
        }

        return context.ErrorCount == errorsBefore;
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        var outcome = await left.TryParseAsync(context, input).ConfigureAwait(false);

        if (!context.ShouldStop)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await right.TryParseAsync(context, input).ConfigureAwait(false);
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<T>(true, outcome.Value)
            : new ParseOutcome<T>(false, default!);
    }
}
