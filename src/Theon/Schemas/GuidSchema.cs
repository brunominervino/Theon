using Theon.Checks;
using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="Guid"/>.
/// </summary>
/// <remarks>
/// There is no parsing to do here: by the time a value is a <see cref="Guid"/>, the runtime has
/// already established it is well formed. What remains is whether it is a meaningful one, which in
/// practice means catching <see cref="Guid.Empty"/> where a real identifier was expected.
/// </remarks>
public sealed class GuidSchema : Schema<Guid>
{
    private readonly Check<Guid>[] _checks;

    internal GuidSchema()
        : this([])
    {
    }

    private GuidSchema(Check<Guid>[] checks) => _checks = checks;

    private GuidSchema With(Check<Guid> check) => new([.. _checks, check]);

    /// <summary>Rejects <see cref="Guid.Empty"/>.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// An all-zero identifier is almost always a field that was never assigned rather than a
    /// deliberate value, and it is the one case the type system cannot catch for you.
    /// </remarks>
    public GuidSchema NotEmpty(string? message = null) =>
        With(new RefineCheck<Guid>(static value => value != Guid.Empty, message ?? "Must not be the empty identifier."));

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public GuidSchema Refine(Func<Guid, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<Guid>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<Guid?> AllowNull() => new NullableValueSchema<Guid>(this);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, Guid input, out Guid output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        foreach (var check in _checks)
        {
            if (context.ShouldStop)
            {
                break;
            }

            check.Run(ref context, ref output);
        }

        return context.ErrorCount == errorsBefore;
    }
}
