namespace Theon;

/// <summary>
/// Thrown when a schema containing an asynchronous rule is parsed synchronously.
/// </summary>
/// <remarks>
/// An asynchronous rule cannot be waited for from a synchronous method without blocking a thread,
/// and blocking one inside a request handler is how a thread-pool starvation incident starts. So
/// the synchronous path refuses outright rather than appearing to work: the schema was built with
/// a rule that needs I/O, and the caller has to await it.
/// </remarks>
public sealed class SchemaAsyncUsageException : InvalidOperationException
{
    internal SchemaAsyncUsageException()
        : base(
            "This schema contains an asynchronous rule and cannot be parsed synchronously. " +
            "Call ParseAsync or SafeParseAsync instead.")
    {
    }
}
