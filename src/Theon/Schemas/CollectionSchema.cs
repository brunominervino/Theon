using Theon.Checks;
using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Validates a list of values, applying an element schema to each entry.
/// </summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <remarks>
/// <para>
/// The input is an <see cref="IReadOnlyList{T}"/> rather than an <see cref="IEnumerable{T}"/>
/// deliberately. Counting an enumerable means walking it, and walking it a second time to validate
/// breaks any sequence that is lazy or single-use — quietly, and only for some callers. A list has
/// a count and an indexer, and arrays, <see cref="List{T}"/> and immutable arrays all are one.
/// </para>
/// <para>
/// Errors carry the element index, so a failure four items in reports as
/// <c>Recipients[3]</c> rather than as a complaint about the list as a whole.
/// </para>
/// </remarks>
public sealed class CollectionSchema<TElement> : Schema<IReadOnlyList<TElement>>
{
    private readonly Schema<TElement, TElement> _element;
    private readonly Check<IReadOnlyList<TElement>>[] _checks;

    internal CollectionSchema(Schema<TElement, TElement> element)
        : this(element, [])
    {
    }

    private CollectionSchema(Schema<TElement, TElement> element, Check<IReadOnlyList<TElement>>[] checks)
    {
        _element = element;
        _checks = checks;
    }

    private CollectionSchema<TElement> With(Check<IReadOnlyList<TElement>> check) =>
        new(_element, [.. _checks, check]);

    /// <summary>Requires at least <paramref name="minimum"/> elements.</summary>
    /// <param name="minimum">The smallest allowed number of elements.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public CollectionSchema<TElement> MinCount(int minimum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return With(new MinCountCheck<TElement>(minimum) { Message = message });
    }

    /// <summary>Requires at most <paramref name="maximum"/> elements.</summary>
    /// <param name="maximum">The largest allowed number of elements.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public CollectionSchema<TElement> MaxCount(int maximum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return With(new MaxCountCheck<TElement>(maximum) { Message = message });
    }

    /// <summary>Requires exactly <paramref name="count"/> elements.</summary>
    /// <param name="count">The required number of elements.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public CollectionSchema<TElement> Count(int count, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return With(new ExactCountCheck<TElement>(count) { Message = message });
    }

    /// <summary>Requires at least one element.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public CollectionSchema<TElement> NotEmpty(string? message = null) => MinCount(1, message);

    /// <summary>Requires the list as a whole to satisfy a predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the list is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public CollectionSchema<TElement> Refine(Func<IReadOnlyList<TElement>, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<IReadOnlyList<TElement>>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<IReadOnlyList<TElement>?> AllowNull() =>
        new NullableReferenceSchema<IReadOnlyList<TElement>>(this);

    /// <inheritdoc />
    /// <remarks>
    /// Elements are awaited in order, not in parallel. A list of twenty addresses each needing a
    /// lookup should not become twenty simultaneous queries because the schema decided to be
    /// clever, and ordered errors are what a caller can match back to the input.
    /// </remarks>
    public override async ValueTask<ParseOutcome<IReadOnlyList<TElement>>> TryParseAsync(
        AsyncParseContext context,
        IReadOnlyList<TElement> input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a list",
                Received = "null",
            });

            return new ParseOutcome<IReadOnlyList<TElement>>(false, Array.Empty<TElement>());
        }

        var value = input;
        var sync = context.BeginSync();
        try
        {
            foreach (var check in _checks)
            {
                if (sync.ShouldStop)
                {
                    break;
                }

                check.Run(ref sync, ref value);
            }
        }
        finally
        {
            context.EndSync(ref sync);
        }

        if (context.ErrorCount != errorsBefore)
        {
            return new ParseOutcome<IReadOnlyList<TElement>>(false, Array.Empty<TElement>());
        }

        for (var i = 0; i < input.Count; i++)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            context.PushIndex(i);
            await _element.TryParseAsync(context, input[i]).ConfigureAwait(false);
            context.Pop();
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<IReadOnlyList<TElement>>(true, value)
            : new ParseOutcome<IReadOnlyList<TElement>>(false, Array.Empty<TElement>());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Count rules run before the elements. A list of the wrong length is a complaint about the
    /// list, and reporting it alongside a hundred element failures buries it.
    /// </remarks>
    public override bool TryParse(
        ref ParseContext context,
        IReadOnlyList<TElement> input,
        out IReadOnlyList<TElement> output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a list",
                Received = "null",
            });

            output = Array.Empty<TElement>();
            return false;
        }

        output = input;

        foreach (var check in _checks)
        {
            if (context.ShouldStop)
            {
                break;
            }

            check.Run(ref context, ref output);
        }

        if (context.ErrorCount != errorsBefore)
        {
            return false;
        }

        for (var i = 0; i < input.Count; i++)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.PushIndex(i);
            _element.TryParse(ref context, input[i], out _);
            context.Pop();
        }

        return context.ErrorCount == errorsBefore;
    }
}
