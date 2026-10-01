using Theon.Checks;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="TimeSpan"/>.
/// </summary>
/// <remarks>
/// <para>
/// A duration, not a point in time, which is why there is no <c>InPast</c> or <c>InFuture</c> here
/// and why its bounds are described the way a number's are: a timeout is greater than five seconds,
/// it is not later than five seconds.
/// </para>
/// <para>
/// A <see cref="TimeSpan"/> can be negative, and that is the mistake this schema mostly exists to
/// catch — a duration computed by subtracting two dates in the wrong order.
/// </para>
/// </remarks>
public sealed class TimeSpanSchema : Schema<TimeSpan>
{
    private readonly Check<TimeSpan>[] _checks;

    internal TimeSpanSchema()
        : this([])
    {
    }

    private TimeSpanSchema(Check<TimeSpan>[] checks) => _checks = checks;

    private TimeSpanSchema With(Check<TimeSpan> check) => new([.. _checks, check]);

    /// <summary>Requires the value to be at least <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The smallest allowed duration.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public TimeSpanSchema Min(TimeSpan minimum, string? message = null) =>
        With(new NotBeforeCheck<TimeSpan>(minimum, inclusive: true, ValidationOrigin.Number)
        {
            Message = message,
        });

    /// <summary>Requires the value to be at most <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The largest allowed duration.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public TimeSpanSchema Max(TimeSpan maximum, string? message = null) =>
        With(new NotAfterCheck<TimeSpan>(maximum, inclusive: true, ValidationOrigin.Number)
        {
            Message = message,
        });

    /// <summary>Requires a duration greater than zero.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>Rejects <see cref="TimeSpan.Zero"/> as well as a negative duration.</remarks>
    public TimeSpanSchema Positive(string? message = null) =>
        With(new NotBeforeCheck<TimeSpan>(TimeSpan.Zero, inclusive: false, ValidationOrigin.Number)
        {
            Message = message,
        });

    /// <summary>Requires a duration of zero or more.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public TimeSpanSchema NonNegative(string? message = null) =>
        With(new NotBeforeCheck<TimeSpan>(TimeSpan.Zero, inclusive: true, ValidationOrigin.Number)
        {
            Message = message,
        });

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public TimeSpanSchema Refine(Func<TimeSpan, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<TimeSpan>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<TimeSpan?> AllowNull() => new NullableValueSchema<TimeSpan>(this);

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.String, _checks);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, TimeSpan input, out TimeSpan output)
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
