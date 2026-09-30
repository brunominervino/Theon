using Theon.Metadata;

namespace Theon.Schemas;

// Substitutes a value for null, and otherwise defers to an inner schema, for a value type.
//
// The mirror image of RequiredValueSchema: both take a schema of T and give back one of T?, and they
// differ only in what they do when the value is absent. One reports; this one answers.
//
// The fallback is not run through the inner schema. There is nothing to learn from doing so — the
// schema's author wrote both — and it would make a misconfigured default fail at parse time, on a
// value the caller never supplied, which is a confusing place to find out.
internal sealed class DefaultValueSchema<T>(Schema<T> inner, T fallback) : Schema<T?, T>
    where T : struct
{
    // Null is accepted and answered, so the document says the value may be null and names what it
    // becomes. "default" is an annotation in this dialect, not an assertion, which is exactly right:
    // it tells a reader what happens without claiming a validator will do it.
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        var described = context.Describe(inner);
        described.AllowsNull = true;
        described.DefaultValue = fallback;
        described.HasDefaultValue = true;
        return described;
    }

    public override bool TryParse(ref ParseContext context, T? input, out T output)
    {
        if (input is null)
        {
            output = fallback;
            return true;
        }

        return inner.TryParse(ref context, input.Value, out output);
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            return new ParseOutcome<T>(true, fallback);
        }

        return await inner.TryParseAsync(context, input.Value).ConfigureAwait(false);
    }
}

// Substitutes a value for null, and otherwise defers to an inner schema, for a reference type.
internal sealed class DefaultReferenceSchema<T>(Schema<T> inner, T fallback) : Schema<T?, T>
    where T : class
{
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        var described = context.Describe(inner);
        described.AllowsNull = true;
        described.DefaultValue = fallback;
        described.HasDefaultValue = true;
        return described;
    }

    public override bool TryParse(ref ParseContext context, T? input, out T output)
    {
        if (input is null)
        {
            output = fallback;
            return true;
        }

        return inner.TryParse(ref context, input, out output);
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            return new ParseOutcome<T>(true, fallback);
        }

        return await inner.TryParseAsync(context, input).ConfigureAwait(false);
    }
}
