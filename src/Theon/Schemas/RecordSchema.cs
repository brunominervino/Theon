using Theon.Checks;
using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a dictionary, applying one schema to every key and another to every value.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <remarks>
/// <para>
/// For the shapes whose keys are data rather than structure: translations by language code, prices
/// by currency, a bag of feature flags. An object schema cannot describe those, because the names
/// are not known when the schema is written.
/// </para>
/// <para>
/// A failure inside a value is reported under the key it belongs to, so a bad price for
/// <c>BRL</c> reads as <c>Prices.BRL</c>.
/// </para>
/// </remarks>
public sealed class RecordSchema<TKey, TValue> : Schema<IReadOnlyDictionary<TKey, TValue>>
    where TKey : notnull
{
    private static readonly IReadOnlyDictionary<TKey, TValue> Empty = new Dictionary<TKey, TValue>();

    private readonly Schema<TKey, TKey> _key;
    private readonly Schema<TValue, TValue> _value;
    private readonly Check<IReadOnlyDictionary<TKey, TValue>>[] _checks;

    internal RecordSchema(Schema<TKey, TKey> key, Schema<TValue, TValue> value)
        : this(key, value, [])
    {
    }

    private RecordSchema(
        Schema<TKey, TKey> key,
        Schema<TValue, TValue> value,
        Check<IReadOnlyDictionary<TKey, TValue>>[] checks)
    {
        _key = key;
        _value = value;
        _checks = checks;
    }

    private RecordSchema<TKey, TValue> With(Check<IReadOnlyDictionary<TKey, TValue>> check) =>
        new(_key, _value, [.. _checks, check]);

    /// <summary>Requires at least <paramref name="minimum"/> entries.</summary>
    /// <param name="minimum">The smallest allowed number of entries.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public RecordSchema<TKey, TValue> MinCount(int minimum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return With(new RecordCountCheck<TKey, TValue>(minimum, null) { Message = message });
    }

    /// <summary>Requires at most <paramref name="maximum"/> entries.</summary>
    /// <param name="maximum">The largest allowed number of entries.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public RecordSchema<TKey, TValue> MaxCount(int maximum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return With(new RecordCountCheck<TKey, TValue>(null, maximum) { Message = message });
    }

    /// <summary>Requires at least one entry.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public RecordSchema<TKey, TValue> NotEmpty(string? message = null) => MinCount(1, message);

    /// <summary>Requires the dictionary as a whole to satisfy a predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the dictionary is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public RecordSchema<TKey, TValue> Refine(
        Func<IReadOnlyDictionary<TKey, TValue>, bool> predicate,
        string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<IReadOnlyDictionary<TKey, TValue>>(predicate, message));
    }

    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = CheckDescription.Of(SchemaKind.Map, _checks);

        // The value schema becomes additionalProperties, which is how a document says "any key, this
        // kind of value". What the key schema requires has nowhere to go: propertyNames exists in the
        // dialect but takes a schema over the text of a key, and ours is a schema over its type.
        description.AdditionalProperties = context.Describe(_value);
        return description;
    }

    /// <inheritdoc />
    public override bool TryParse(
        ref ParseContext context,
        IReadOnlyDictionary<TKey, TValue> input,
        out IReadOnlyDictionary<TKey, TValue> output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a dictionary",
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

        foreach (var entry in input)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.PushProperty(entry.Key.ToString() ?? string.Empty);
            _key.TryParse(ref context, entry.Key, out _);
            _value.TryParse(ref context, entry.Value, out _);
            context.Pop();
        }

        return context.ErrorCount == errorsBefore;
    }

    /// <inheritdoc />
    public override async ValueTask<ParseOutcome<IReadOnlyDictionary<TKey, TValue>>> TryParseAsync(
        AsyncParseContext context,
        IReadOnlyDictionary<TKey, TValue> input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a dictionary",
                Received = "null",
            });

            return new ParseOutcome<IReadOnlyDictionary<TKey, TValue>>(false, Empty);
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
            return new ParseOutcome<IReadOnlyDictionary<TKey, TValue>>(false, Empty);
        }

        foreach (var entry in input)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            context.PushProperty(entry.Key.ToString() ?? string.Empty);
            await _key.TryParseAsync(context, entry.Key).ConfigureAwait(false);
            await _value.TryParseAsync(context, entry.Value).ConfigureAwait(false);
            context.Pop();
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<IReadOnlyDictionary<TKey, TValue>>(true, value)
            : new ParseOutcome<IReadOnlyDictionary<TKey, TValue>>(false, Empty);
    }
}
