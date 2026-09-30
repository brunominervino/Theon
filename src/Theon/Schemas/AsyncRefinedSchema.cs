using Theon.Errors;

namespace Theon.Schemas;

// Wraps any schema with a rule that has to await something: a lookup, a call, a query.
//
// The synchronous path throws rather than blocking. Waiting on I/O from a synchronous method
// occupies a thread doing nothing, and doing that inside a request handler is how thread-pool
// starvation starts. A schema built with an asynchronous rule has to be awaited, and saying so
// loudly at the first call beats a deadlock under load.
internal sealed class AsyncRefinedSchema<T>(
    Schema<T> inner,
    Func<T, CancellationToken, ValueTask<bool>> predicate,
    string message) : Schema<T>
{
    public override bool TryParse(ref ParseContext context, T input, out T output) =>
        throw new SchemaAsyncUsageException();

    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var inner_ = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        if (!inner_.Succeeded)
        {
            return new ParseOutcome<T>(false, default!);
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        var satisfied = await predicate(inner_.Value, context.CancellationToken).ConfigureAwait(false);
        if (satisfied)
        {
            return new ParseOutcome<T>(true, inner_.Value);
        }

        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, message);
        return new ParseOutcome<T>(false, default!);
    }
}
