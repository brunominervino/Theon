using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Theon.Schemas;

namespace Theon;

/// <summary>
/// The entry point for building schemas.
/// </summary>
/// <remarks>
/// Everything starts here, so that typing <c>Schema.</c> in an editor lists the whole vocabulary.
/// </remarks>
/// <example>
/// <code>
/// private static readonly Schema&lt;CreateUser&gt; Validator =
///     Theo.Object&lt;CreateUser&gt;()
///         .Field(x =&gt; x.Name, Theo.String().Trim().MinLength(3).MaxLength(100))
///         .Field(x =&gt; x.Email, Theo.String().Trim().ToLowerInvariant().Email())
///         .Field(x =&gt; x.Age, Theo.Int().Min(18).Max(120))
///         .Field(x =&gt; x.CompanyId, Theo.Guid().NotEmpty());
///
/// var result = Validator.SafeParse(request);
/// if (!result.IsSuccess)
/// {
///     foreach (var error in result.Errors)
///     {
///         Console.WriteLine($"{error.Path}: {error.Message}");
///     }
/// }
/// </code>
/// </example>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification =
        "Naming the type is precisely what these factories do, and the type name is the most " +
        "discoverable thing to call them. Theo.String() reads better than any circumlocution " +
        "the rule would accept, and this is the most-used surface in the library.")]
public static class Theo
{
    /// <summary>Starts a schema for a <see cref="string"/>.</summary>
    public static StringSchema String() => new();

    /// <summary>Starts a schema for an <see cref="int"/>.</summary>
    public static NumberSchema<int> Int() => new();

    /// <summary>Starts a schema for a <see cref="long"/>.</summary>
    public static NumberSchema<long> Long() => new();

    /// <summary>Starts a schema for a <see cref="decimal"/>.</summary>
    /// <remarks>Prefer this over <see cref="Double"/> for money and anything else counted in decimal steps.</remarks>
    public static NumberSchema<decimal> Decimal() => new();

    /// <summary>Starts a schema for a <see cref="double"/>.</summary>
    public static NumberSchema<double> Double() => new();

    /// <summary>Starts a schema for any numeric type.</summary>
    /// <typeparam name="T">The numeric type, such as <see cref="short"/>, <see cref="byte"/> or <see cref="System.Half"/>.</typeparam>
    public static NumberSchema<T> Number<T>()
        where T : struct, INumber<T> => new();

    /// <summary>Starts a schema for a <see cref="System.Guid"/>.</summary>
    public static GuidSchema Guid() => new();

    /// <summary>Starts a schema for an object of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The object type to validate: a class, record, struct or record struct.</typeparam>
    public static ObjectSchema<T> Object<T>()
        where T : notnull => new();

    /// <summary>Starts a schema for a list, applying <paramref name="element"/> to each entry.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="element">The schema every element must satisfy.</param>
    /// <example>
    /// <code>
    /// Theo.Collection(Theo.String().Email()).MinCount(1).MaxCount(10);
    /// </code>
    /// </example>
    public static CollectionSchema<TElement> Collection<TElement>(Schema<TElement, TElement> element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return new CollectionSchema<TElement>(element);
    }

    /// <summary>Starts a schema for an enum, rejecting values the type does not declare.</summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <remarks>
    /// Worth more than it looks: <c>(TEnum)999</c> is a legal cast, so an enum-typed value is not
    /// evidence that the value is one of the members.
    /// </remarks>
    public static EnumSchema<TEnum> Enum<TEnum>()
        where TEnum : struct, System.Enum => new();

    /// <summary>Starts a schema for a <see cref="System.DateTime"/>.</summary>
    /// <param name="timeProvider">
    /// The clock used by <c>InPast</c> and <c>InFuture</c>. Defaults to
    /// <see cref="TimeProvider.System"/>; pass a fake one in tests.
    /// </param>
    public static DateTimeSchema DateTime(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Starts a schema for a <see cref="System.DateTimeOffset"/>.</summary>
    /// <param name="timeProvider">The clock used by <c>InPast</c> and <c>InFuture</c>.</param>
    public static DateTimeOffsetSchema DateTimeOffset(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Starts a schema for a <see cref="System.DateOnly"/>.</summary>
    /// <param name="timeProvider">The clock used by <c>InPast</c> and <c>InFuture</c>.</param>
    public static DateOnlySchema DateOnly(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    /// <summary>Starts a schema for a <see cref="System.TimeOnly"/>.</summary>
    public static TimeOnlySchema TimeOnly() => new(TimeProvider.System);

    /// <summary>
    /// Defers building a schema until it is first used, so that it can refer to itself.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="schema">Builds the schema. Runs once, on the first parse.</param>
    /// <remarks>
    /// <para>
    /// A comment with replies, a category with subcategories, an expression with operands — any
    /// shape that contains itself. Such a schema cannot be written directly, because the
    /// declaration would have to name itself before it exists. This breaks the cycle by deferring.
    /// </para>
    /// <para>
    /// Recursion makes unbounded depth reachable, and a value that refers to itself makes it
    /// infinite, so a parse that descends past
    /// <see cref="ParseOptions.MaxDepth"/> reports an error rather than overflowing the stack.
    /// </para>
    /// <para>
    /// Have the factory hand back a schema that already exists rather than build a new one. Each
    /// call to this method makes its own wrapper, so a factory that constructs from scratch builds
    /// a fresh schema for every level of the value — correct, and wasteful on a deep one. Pointing
    /// it at a static field costs one construction no matter how deep the value goes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly Schema&lt;Comment&gt; CommentSchema =
    ///     Theo.Object&lt;Comment&gt;()
    ///         .Field(x =&gt; x.Body, Theo.String().NotEmpty())
    ///         .Field(x =&gt; x.Replies, Theo.Collection(Theo.Lazy(() =&gt; CommentSchema)));
    /// </code>
    /// </example>
    public static Schema<T> Lazy<T>(Func<Schema<T>> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new LazySchema<T>(schema);
    }

    /// <summary>
    /// Starts a schema for a dictionary whose keys are data rather than structure.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="key">The schema every key must satisfy.</param>
    /// <param name="value">The schema every value must satisfy.</param>
    /// <example>
    /// <code>
    /// // Prices by currency code, where the codes are not known in advance.
    /// Theo.Record(Theo.String().Length(3).Uppercase(), Theo.Decimal().Positive());
    /// </code>
    /// </example>
    public static RecordSchema<TKey, TValue> Record<TKey, TValue>(
        Schema<TKey, TKey> key,
        Schema<TValue, TValue> value)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return new RecordSchema<TKey, TValue>(key, value);
    }

    /// <summary>
    /// Starts a schema for a dictionary keyed by <see cref="string"/>.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="value">The schema every value must satisfy.</param>
    /// <remarks>Keys are accepted as they come; use the other overload to constrain them.</remarks>
    public static RecordSchema<string, TValue> Record<TValue>(Schema<TValue, TValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RecordSchema<string, TValue>(new StringSchema(), value);
    }

    /// <summary>
    /// Accepts a value that satisfies any one of <paramref name="alternatives"/>.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="message">The message to report when none of them is satisfied.</param>
    /// <param name="alternatives">The schemas to try, in order. The first to succeed wins.</param>
    /// <remarks>
    /// <para>
    /// A single message is reported rather than the errors from each branch. "Not an e-mail, and
    /// not a phone number" is two complaints about one field where the reader wanted one, and the
    /// branch errors describe alternatives the caller never chose.
    /// </para>
    /// <para>
    /// Each alternative is tried in isolation, so a failed attempt leaves nothing behind.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.OneOf(
    ///     "Enter an e-mail address or a phone number.",
    ///     Theo.String().Email(),
    ///     Theo.String().Matches(PhoneNumber(), "phone"));
    /// </code>
    /// </example>
    public static Schema<T> OneOf<T>(string message, params Schema<T>[] alternatives)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        ArgumentNullException.ThrowIfNull(alternatives);

        if (alternatives.Length < 2)
        {
            throw new ArgumentException(
                "A choice needs at least two alternatives; with one, use that schema directly.",
                nameof(alternatives));
        }

        return new OneOfSchema<T>([.. alternatives], message);
    }
}
