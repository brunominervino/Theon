using Theon.Errors;
using Theon.Metadata;

namespace Theon.Schemas;

// Runs an inner schema, then attempts to convert its result, then optionally validates the result of
// that.
//
// The difference from TransformSchema is that the conversion is allowed to fail. Turning text into a
// number is the ordinary case and it fails all the time -- a query string is whatever the caller put
// in it -- so the conversion reports rather than throws, and the failure lands at the path the value
// came from like any other.
internal sealed class TryTransformSchema<TInput, TIntermediate, TOutput>(
    Schema<TInput, TIntermediate> inner,
    TransformAttempt<TIntermediate, TOutput> attempt,
    string message,
    Schema<TOutput>? then) : Schema<TInput, TOutput>
{
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var described = context.Describe(inner);

        // A document says what the caller sends, which is the input side. The conversion and anything
        // checked after it constrain what this program made of that, and there is no keyword for
        // "the number this text parses to is between one and a hundred".
        described.CannotRepresent(then is null ? "TryTransform" : "TryTransform and its follow-on rules");
        return described;
    }

    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output)
    {
        if (!inner.TryParse(ref context, input, out var intermediate))
        {
            output = default!;
            return false;
        }

        if (!attempt(intermediate, out var converted))
        {
            context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidType }, message);
            output = default!;
            return false;
        }

        if (then is null)
        {
            output = converted;
            return true;
        }

        var succeeded = then.TryParse(ref context, converted, out var checked_);
        output = succeeded ? checked_ : default!;
        return succeeded;
    }

    public override async ValueTask<ParseOutcome<TOutput>> TryParseAsync(
        AsyncParseContext context,
        TInput input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        if (!outcome.Succeeded)
        {
            return new ParseOutcome<TOutput>(false, default!);
        }

        if (!attempt(outcome.Value, out var converted))
        {
            context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidType }, message);
            return new ParseOutcome<TOutput>(false, default!);
        }

        return then is null
            ? new ParseOutcome<TOutput>(true, converted)
            : await then.TryParseAsync(context, converted).ConfigureAwait(false);
    }
}
