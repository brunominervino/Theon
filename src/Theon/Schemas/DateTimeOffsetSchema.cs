using Theon.Checks;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="DateTimeOffset"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="DateTime"/>, this type carries its offset, so two values from different zones
/// compare as the instants they actually represent. There is no kind to check and no ambiguity to
/// defend against, which makes it the better choice at an API boundary.
/// </remarks>
public sealed class DateTimeOffsetSchema : Schema<DateTimeOffset>
{
    private readonly Check<DateTimeOffset>[] _checks;
    private readonly TimeProvider _timeProvider;

    internal DateTimeOffsetSchema(TimeProvider timeProvider)
        : this(timeProvider, [])
    {
    }

    private DateTimeOffsetSchema(TimeProvider timeProvider, Check<DateTimeOffset>[] checks)
    {
        _timeProvider = timeProvider;
        _checks = checks;
    }

    private DateTimeOffsetSchema With(Check<DateTimeOffset> check) => new(_timeProvider, [.. _checks, check]);

    /// <summary>Requires the value to be at or after <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The earliest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeOffsetSchema Min(DateTimeOffset minimum, string? message = null) =>
        With(new NotBeforeCheck<DateTimeOffset>(minimum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be at or before <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The latest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeOffsetSchema Max(DateTimeOffset maximum, string? message = null) =>
        With(new NotAfterCheck<DateTimeOffset>(maximum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be strictly earlier than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeOffsetSchema InPast(string? message = null) =>
        With(new RelativeToNowCheck<DateTimeOffset>(
            _timeProvider,
            static provider => provider.GetUtcNow(),
            mustBeBefore: true,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to be strictly later than now.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public DateTimeOffsetSchema InFuture(string? message = null) =>
        With(new RelativeToNowCheck<DateTimeOffset>(
            _timeProvider,
            static provider => provider.GetUtcNow(),
            mustBeBefore: false,
            inclusive: false)
        { Message = message });

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public DateTimeOffsetSchema Refine(Func<DateTimeOffset, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<DateTimeOffset>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<DateTimeOffset?> AllowNull() => new NullableValueSchema<DateTimeOffset>(this);

    internal override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.String, _checks, "date-time");

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, DateTimeOffset input, out DateTimeOffset output)
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
