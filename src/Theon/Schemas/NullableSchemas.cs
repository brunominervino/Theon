using Theon.Metadata;

namespace Theon.Schemas;

// Accepts null, and otherwise defers to an inner schema for a reference type.
// The non-null type validated by the inner schema.
// C# has no separate notion of "absent" as distinct from "null", so there is one wrapper here
// where a TypeScript library needs two. See docs/decisions/0004-nullability.md.
internal sealed class NullableReferenceSchema<T>(Schema<T> inner) : Schema<T?>
    where T : class
{
    // Null goes in and null comes out, so both sides of the description say so. Unlike Default, which
    // accepts a null and answers with something else, this one hands the null straight back.
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        return new SchemaDescription(context.Describe(inner)) { AllowsNull = true };
    }

    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            output = null;
            return true;
        }

        var succeeded = inner.TryParse(ref context, input, out var parsed);
        output = parsed;
        return succeeded;
    }

    public override async ValueTask<ParseOutcome<T?>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            return new ParseOutcome<T?>(true, null);
        }

        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        return new ParseOutcome<T?>(outcome.Succeeded, outcome.Value);
    }
}

// Accepts null, and otherwise defers to an inner schema for a value type.
// The underlying value type validated by the inner schema.
internal sealed class NullableValueSchema<T>(Schema<T> inner) : Schema<T?>
    where T : struct
{
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        return new SchemaDescription(context.Describe(inner)) { AllowsNull = true };
    }

    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            output = null;
            return true;
        }

        var succeeded = inner.TryParse(ref context, input.Value, out var parsed);
        output = parsed;
        return succeeded;
    }

    public override async ValueTask<ParseOutcome<T?>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            return new ParseOutcome<T?>(true, null);
        }

        var outcome = await inner.TryParseAsync(context, input.Value).ConfigureAwait(false);
        return new ParseOutcome<T?>(outcome.Succeeded, outcome.Value);
    }
}
