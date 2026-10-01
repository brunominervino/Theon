using Theon.Checks;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="TimeOnly"/>.
/// </summary>
/// <remarks>
/// There is no <c>InPast</c> or <c>InFuture</c> here. A time of day is cyclic: 09:00 is both
/// earlier and later than 17:00 depending on which day each one falls on, so comparing one against
/// "now" answers a question nobody asked.
/// </remarks>
public sealed class TimeOnlySchema : Schema<TimeOnly>
{
    private readonly Check<TimeOnly>[] _checks;
    private readonly TimeProvider _timeProvider;

    internal TimeOnlySchema(TimeProvider timeProvider)
        : this(timeProvider, [])
    {
    }

    private TimeOnlySchema(TimeProvider timeProvider, Check<TimeOnly>[] checks)
    {
        _timeProvider = timeProvider;
        _checks = checks;
    }

    private TimeOnlySchema With(Check<TimeOnly> check) => new(_timeProvider, [.. _checks, check]);

    /// <summary>Requires the value to be at or after <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The earliest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public TimeOnlySchema Min(TimeOnly minimum, string? message = null) =>
        With(new NotBeforeCheck<TimeOnly>(minimum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be at or before <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The latest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public TimeOnlySchema Max(TimeOnly maximum, string? message = null) =>
        With(new NotAfterCheck<TimeOnly>(maximum, inclusive: true) { Message = message });

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public TimeOnlySchema Refine(Func<TimeOnly, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<TimeOnly>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<TimeOnly?> AllowNull() => new NullableValueSchema<TimeOnly>(this);

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.String, _checks, "time");

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, TimeOnly input, out TimeOnly output)
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
