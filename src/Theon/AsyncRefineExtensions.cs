using Theon.Schemas;

namespace Theon;

/// <summary>
/// Adds a rule that has to await something.
/// </summary>
public static class AsyncRefineExtensions
{
    /// <summary>
    /// Requires the value to satisfy a predicate that performs I/O.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="schema">The schema to extend.</param>
    /// <param name="predicate">
    /// Returns <see langword="true"/> when the value is acceptable. Receives the parse's
    /// cancellation token and should pass it on to whatever it calls.
    /// </param>
    /// <param name="message">The message to report when it is not.</param>
    /// <remarks>
    /// <para>
    /// This is for the rules that cannot be answered from the value alone: whether an address is
    /// already registered, whether a code exists, whether a document is on a list. They need a
    /// round trip, and a round trip needs awaiting.
    /// </para>
    /// <para>
    /// A schema containing one of these throws <see cref="SchemaAsyncUsageException"/> if parsed
    /// synchronously, rather than blocking a thread to hide the difference.
    /// </para>
    /// <para>
    /// It runs only after everything before it has passed, so the predicate never sees a value
    /// already known to be wrong — and, more to the point, a malformed address never costs a
    /// database round trip.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.String().Trim().Email().RefineAsync(
    ///     (email, ct) => users.IsAvailableAsync(email, ct),
    ///     "That address is already registered.");
    /// </code>
    /// </example>
    public static Schema<T> RefineAsync<T>(
        this Schema<T> schema,
        Func<T, CancellationToken, ValueTask<bool>> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);

        return new AsyncRefinedSchema<T>(schema, predicate, message);
    }
}
