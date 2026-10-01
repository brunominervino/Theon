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
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Direction == DescriptionDirection.Output)
        {
            return DescribeOutput(context);
        }

        // The input side of the chain is what a caller sends. What the transform turns it into is this
        // program's business and not the caller's.
        var described = context.Describe(inner);

        // Rules applied after the transform constrain the converted value, and the dialect has no way
        // to say "the number this text parses to is between one and a hundred". Recorded rather than
        // dropped, so that a caller who asked to be told can be.
        return then is null
            ? described
            : described.CannotRepresent("rules applied after Transform");
    }

    // What comes out, when the caller asked about that side.
    //
    // With a follow-on schema this is the one place a transform is fully describable: the schema that
    // checks the converted value is a schema, so it describes itself and nothing is left out.
    //
    // Without one there is a type and no schema, which is the price of handing over a function. The
    // type is still a fact this library owns, so the kind it travels as is written down -- that is
    // incomplete rather than invented. What is lost is recorded either way, because a document whose
    // response side says only "an integer" is not a contract, and that is the argument for reaching
    // for the overload that takes a schema.
    private SchemaDescription DescribeOutput(DescriptionContext context)
    {
        if (then is not null)
        {
            return context.Describe(then);
        }

        return new SchemaDescription
        {
            Kind = SchemaKinds.For(typeof(TOutput)),
            Unrepresentable = ["the output of Transform, which is a type and not a schema"],
        };
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
