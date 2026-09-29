using Theon.Checks;
using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="DateTime"/>.
/// </summary>
/// <remarks>
/// <para>
/// Comparisons here are plain <see cref="DateTime"/> comparisons, which ignore
/// <see cref="DateTime.Kind"/> entirely: a local value and a UTC value compare as though they were
/// on the same clock, and disagree by the machine's offset without any error. Where the values
/// come from outside the process, pair a bound with <see cref="RequireUtc"/> so that assumption is
/// checked rather than hoped for.
/// </para>
/// <para>
/// <see cref="InPast"/> and <see cref="InFuture"/> read the clock from a
/// <see cref="TimeProvider"/>, so they can be tested against a fixed instant instead of whatever
/// the machine happens to think the time is.
/// </para>
/// </remarks>
public sealed class DateTimeSchema : Schema<DateTime>
{
    private readonly Check<DateTime>[] _checks;
    private readonly TimeProvider _timeProvider;

    internal DateTimeSchema(TimeProvider timeProvider)
        : this(timeProvider, [])
    {
    }

    private DateTimeSchema(TimeProvider timeProvider, Check<DateTime>[] checks)
    {
        _timeProvider = timeProvider;
        _checks = checks;
    }

    private DateTimeSchema With(Check<DateTime> check) => new(_timeProvider, [.. _checks, check]);

    /// <summary>Requires the value to be at or after <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The earliest allowed instant.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeSchema Min(DateTime minimum, string? message = null) =>
        With(new NotBeforeCheck<DateTime>(minimum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be at or before <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The latest allowed instant.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeSchema Max(DateTime maximum, string? message = null) =>
        With(new NotAfterCheck<DateTime>(maximum, inclusive: true) { Message = message });

    /// <summary>Requires <see cref="DateTime.Kind"/> to be <see cref="DateTimeKind.Utc"/>.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// A failure here stops the remaining rules on this value: every bound below it would be
    /// comparing against an instant that does not mean what it appears to.
    /// </remarks>
    public DateTimeSchema RequireUtc(string? message = null) =>
        With(new RequireKindCheck(DateTimeKind.Utc) { Message = message });

    /// <summary>Requires <see cref="DateTime.Kind"/> to be <see cref="DateTimeKind.Local"/>.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeSchema RequireLocal(string? message = null) =>
        With(new RequireKindCheck(DateTimeKind.Local) { Message = message });

    /// <summary>Requires the value to be strictly earlier than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeSchema InPast(string? message = null) =>
        With(new RelativeToNowCheck<DateTime>(
            _timeProvider,
            static provider => provider.GetUtcNow().UtcDateTime,
            mustBeBefore: true,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to be strictly later than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeSchema InFuture(string? message = null) =>
        With(new RelativeToNowCheck<DateTime>(
            _timeProvider,
            static provider => provider.GetUtcNow().UtcDateTime,
            mustBeBefore: false,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public DateTimeSchema Refine(Func<DateTime, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<DateTime>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<DateTime?> AllowNull() => new NullableValueSchema<DateTime>(this);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, DateTime input, out DateTime output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        var aborted = false;
        foreach (var check in _checks)
        {
            if (aborted || context.ShouldStop)
            {
                break;
            }

            var before = context.ErrorCount;
            check.Run(ref context, ref output);

            if (context.ErrorCount > before && check.AbortsOnFailure)
            {
                aborted = true;
            }
        }

        return context.ErrorCount == errorsBefore;
    }
}
