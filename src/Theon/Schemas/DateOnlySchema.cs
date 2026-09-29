using Theon.Checks;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="DateOnly"/>.
/// </summary>
/// <remarks>
/// "Now" for a date is read in UTC. A caller validating birthdates near a day boundary in a distant
/// zone should supply a <see cref="TimeProvider"/> that reflects the zone they mean.
/// </remarks>
public sealed class DateOnlySchema : Schema<DateOnly>
{
    private readonly Check<DateOnly>[] _checks;
    private readonly TimeProvider _timeProvider;

    internal DateOnlySchema(TimeProvider timeProvider)
        : this(timeProvider, [])
    {
    }

    private DateOnlySchema(TimeProvider timeProvider, Check<DateOnly>[] checks)
    {
        _timeProvider = timeProvider;
        _checks = checks;
    }

    private DateOnlySchema With(Check<DateOnly> check) => new(_timeProvider, [.. _checks, check]);

    /// <summary>Requires the value to be at or after <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The earliest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateOnlySchema Min(DateOnly minimum, string? message = null) =>
        With(new NotBeforeCheck<DateOnly>(minimum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be at or before <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The latest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateOnlySchema Max(DateOnly maximum, string? message = null) =>
        With(new NotAfterCheck<DateOnly>(maximum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be strictly earlier than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateOnlySchema InPast(string? message = null) =>
        With(new RelativeToNowCheck<DateOnly>(
            _timeProvider,
            static provider => DateOnly.FromDateTime(provider.GetUtcNow().UtcDateTime),
            mustBeBefore: true,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to be strictly later than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateOnlySchema InFuture(string? message = null) =>
        With(new RelativeToNowCheck<DateOnly>(
            _timeProvider,
            static provider => DateOnly.FromDateTime(provider.GetUtcNow().UtcDateTime),
            mustBeBefore: false,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public DateOnlySchema Refine(Func<DateOnly, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<DateOnly>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<DateOnly?> AllowNull() => new NullableValueSchema<DateOnly>(this);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, DateOnly input, out DateOnly output)
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
