using Theon.Errors;

namespace Theon.Schemas;

// Accepts a value that satisfies any one of several schemas: an identifier that may be an e-mail
// or a phone number, a code in one of two formats.
//
// Each alternative is tried on a private context, so a failed attempt leaves no trace. Only when
// every one has failed is a single error reported, and it is the message given here rather than
// the errors from each branch. "Not an e-mail, and not a phone number, and not a tax number" is
// three complaints about one field where the reader wanted one: "must be an e-mail or a phone
// number". The branch errors are diagnostics for whoever wrote the schema, not for whoever filled
// in the form.
internal sealed class OneOfSchema<T>(Schema<T>[] alternatives, string message) : Schema<T>
{
    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        foreach (var alternative in alternatives)
        {
            var attempt = new ParseContext(context.Options);
            if (alternative.TryParse(ref attempt, input, out var parsed) && !attempt.HasErrors)
            {
                output = parsed;
                return true;
            }
        }

        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidValue }, message);
        output = default!;
        return false;
    }

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var alternative in alternatives)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var attempt = new AsyncParseContext(context.Options, context.CancellationToken);
            var outcome = await alternative.TryParseAsync(attempt, input).ConfigureAwait(false);

            if (outcome.Succeeded && !attempt.HasErrors)
            {
                return outcome;
            }
        }

        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidValue }, message);
        return new ParseOutcome<T>(false, default!);
    }
}
