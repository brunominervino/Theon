using Theon.Checks;
using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a set of values, applying an element schema to each member.
/// </summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <remarks>
/// <para>
/// The input is an <see cref="IReadOnlyCollection{T}"/>, which <see cref="HashSet{T}"/>,
/// <see cref="IReadOnlySet{T}"/>, <c>FrozenSet</c> and <c>ImmutableHashSet</c> all are. Declare the
/// property as one of those; <c>ISet&lt;T&gt;</c> descends from <c>ICollection&lt;T&gt;</c>, which
/// is not a read-only collection, so it will not bind here.
/// </para>
/// <para>
/// A member's failure is reported at the set's own path, with no index. That is not a shortcut: a
/// set has no positions, and its enumeration order is not something a caller can rely on, so
/// <c>Tags[2]</c> would name a different member on the next run. One error at <c>Tags</c> per
/// unacceptable member is the honest shape, and it is also what a form needs in order to mark the
/// field.
/// </para>
/// <para>
/// There is no <c>Unique</c> here, because a set is already distinct. That rule belongs on
/// <see cref="CollectionSchema{TElement}"/>, where duplicates are possible.
/// </para>
/// <para>
/// This is the one schema that allocates on the success path, and it allocates exactly once: walking
/// a set through an interface boxes its enumerator, and unlike a list a set has no indexer to reach
/// for instead. It is measured in <c>benchmarks/</c>.
/// </para>
/// </remarks>
public sealed class SetSchema<TElement> : Schema<IReadOnlyCollection<TElement>>
{
    private static readonly IReadOnlyCollection<TElement> Empty = Array.Empty<TElement>();

    private readonly Schema<TElement, TElement> _element;
    private readonly Check<IReadOnlyCollection<TElement>>[] _checks;

    internal SetSchema(Schema<TElement, TElement> element)
        : this(element, [])
    {
    }

    private SetSchema(
        Schema<TElement, TElement> element,
        Check<IReadOnlyCollection<TElement>>[] checks)
    {
        _element = element;
        _checks = checks;
    }

    private SetSchema<TElement> With(Check<IReadOnlyCollection<TElement>> check) =>
        new(_element, [.. _checks, check]);

    /// <summary>Requires at least <paramref name="minimum"/> members.</summary>
    /// <param name="minimum">The smallest allowed number of members.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public SetSchema<TElement> MinCount(int minimum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return With(new MinCountCheck<IReadOnlyCollection<TElement>, TElement>(minimum)
        {
            Message = message,
        });
    }

    /// <summary>Requires at most <paramref name="maximum"/> members.</summary>
    /// <param name="maximum">The largest allowed number of members.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public SetSchema<TElement> MaxCount(int maximum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return With(new MaxCountCheck<IReadOnlyCollection<TElement>, TElement>(maximum)
        {
            Message = message,
        });
    }

    /// <summary>Requires exactly <paramref name="count"/> members.</summary>
    /// <param name="count">The required number of members.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public SetSchema<TElement> Count(int count, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return With(new ExactCountCheck<IReadOnlyCollection<TElement>, TElement>(count)
        {
            Message = message,
        });
    }

    /// <summary>Requires at least one member.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public SetSchema<TElement> NotEmpty(string? message = null) => MinCount(1, message);

    /// <summary>Requires the set as a whole to satisfy a predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the set is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public SetSchema<TElement> Refine(
        Func<IReadOnlyCollection<TElement>, bool> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<IReadOnlyCollection<TElement>>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<IReadOnlyCollection<TElement>?> AllowNull() =>
        new NullableReferenceSchema<IReadOnlyCollection<TElement>>(this);

    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = CheckDescription.Of(SchemaKind.Array, _checks);

        // Distinctness is stated when the set is described rather than contributed by a rule, because
        // a set is distinct by construction and has no such rule to contribute it.
        description.UniqueItems = true;
        description.Items = context.Describe(_element);
        return description;
    }

    /// <inheritdoc />
    public override async ValueTask<ParseOutcome<IReadOnlyCollection<TElement>>> TryParseAsync(
        AsyncParseContext context,
        IReadOnlyCollection<TElement> input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a set",
                Received = "null",
            });

            return new ParseOutcome<IReadOnlyCollection<TElement>>(false, Empty);
        }

        var value = input;

        var sync = context.BeginSync();
        foreach (var check in _checks)
        {
            if (sync.ShouldStop)
            {
                break;
            }

            check.Run(ref sync, ref value);
        }

        context.EndSync(ref sync);

        if (context.ErrorCount != errorsBefore)
        {
            return new ParseOutcome<IReadOnlyCollection<TElement>>(false, Empty);
        }

        foreach (var element in input)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            await _element.TryParseAsync(context, element).ConfigureAwait(false);
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<IReadOnlyCollection<TElement>>(true, value)
            : new ParseOutcome<IReadOnlyCollection<TElement>>(false, Empty);
    }

    /// <inheritdoc />
    public override bool TryParse(
        ref ParseContext context,
        IReadOnlyCollection<TElement> input,
        out IReadOnlyCollection<TElement> output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a set",
                Received = "null",
            });

            output = Empty;
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

        foreach (var element in input)
        {
            if (context.ShouldStop)
            {
                break;
            }

            _element.TryParse(ref context, element, out _);
        }

        return context.ErrorCount == errorsBefore;
    }
}
