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

    /// <summary>
    /// Builds a schema from a JSON Schema document, for validating JSON somebody else described.
    /// </summary>
    /// <param name="document">The document, in the 2020-12 dialect.</param>
    /// <returns>A schema over an already-parsed JSON value.</returns>
    /// <remarks>
    /// <para>
    /// The reverse of <c>ToJsonSchema</c>, and for a different job. This is for checking a payload
    /// against a schema somebody else published — a third party's OpenAPI description, a contract test,
    /// a configuration file whose shape is declared elsewhere. It is not a replacement for
    /// <see cref="Object{T}"/>: it produces a <see cref="System.Text.Json.Nodes.JsonNode"/> and not a
    /// typed object, because turning a document into a type would mean matching property names by
    /// reflection, which this library does not do.
    /// </para>
    /// <para>
    /// The input is a <see cref="System.Text.Json.Nodes.JsonNode"/> rather than text, so
    /// <c>System.Text.Json</c> still does the parsing and this still validates a materialized value —
    /// which is what every other schema here does.
    /// </para>
    /// <para>
    /// Supported: <c>$ref</c> and <c>$defs</c> (including a schema that refers to itself),
    /// <c>type</c>, <c>enum</c>, <c>const</c>, <c>minimum</c>, <c>maximum</c>,
    /// <c>exclusiveMinimum</c>, <c>exclusiveMaximum</c>, <c>multipleOf</c>, <c>minLength</c>,
    /// <c>maxLength</c>, <c>pattern</c>, <c>format</c>, <c>items</c>, <c>minItems</c>,
    /// <c>maxItems</c>, <c>uniqueItems</c>, <c>properties</c>, <c>required</c>,
    /// <c>additionalProperties</c>, <c>minProperties</c>, <c>maxProperties</c>, <c>anyOf</c> and
    /// <c>allOf</c>.
    /// </para>
    /// <para>
    /// A keyword that asserts something this cannot check — <c>oneOf</c>, <c>not</c>, <c>if</c>,
    /// <c>patternProperties</c> and the rest — throws here, when the schema is built, rather than being
    /// left out. Leaving an assertion out would make the schema accept values the document rejects,
    /// which is the direction that lets a bad value through while the caller believes it was checked.
    /// A keyword that asserts nothing — a title, a description, an unknown extension — is ignored, as
    /// the dialect requires.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var published = JsonNode.Parse(await client.GetStringAsync(schemaUrl));
    /// var schema = Theo.JsonSchema(published!);
    ///
    /// var result = schema.SafeParse(JsonNode.Parse(payload));
    /// </code>
    /// </example>
    /// <exception cref="System.ArgumentNullException"><paramref name="document"/> was null.</exception>
    /// <exception cref="System.NotSupportedException">
    /// The document uses a keyword this cannot honour, refers outside itself, or carries a pattern that
    /// cannot be matched in linear time.
    /// </exception>
    public static Schema<System.Text.Json.Nodes.JsonNode?> JsonSchema(
        System.Text.Json.Nodes.JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var read = Json.JsonSchemaReader.Read(document);

        // Each definition becomes a schema of its own, sharing one map. A reference resolves through
        // the map at parse time, which is what lets a schema contain itself without this method
        // recursing while it builds one.
        var referenced = new Dictionary<string, Schemas.JsonDocumentSchema>(StringComparer.Ordinal);

        foreach (var (pointer, description) in read.Definitions)
        {
            referenced[pointer] = new Schemas.JsonDocumentSchema(description, referenced);
        }

        return new Schemas.JsonDocumentSchema(read.Root, referenced);
    }

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

    /// <summary>Starts a schema for a <see cref="System.TimeSpan"/>.</summary>
    /// <remarks>
    /// A duration rather than a point in time, so its bounds read like a number's. The rule worth
    /// reaching for most often is <c>Positive()</c>: a <see cref="System.TimeSpan"/> can be negative,
    /// and a negative one is usually two dates subtracted the wrong way round.
    /// </remarks>
    public static TimeSpanSchema TimeSpan() => new();

    /// <summary>Starts a schema for a <see cref="System.Uri"/>.</summary>
    /// <remarks>
    /// For a value that already is a <see cref="System.Uri"/>. Where it is text that ought to be a web
    /// address, <c>Theo.String().Url()</c> answers that question instead, and without constructing
    /// anything.
    /// </remarks>
    public static UriSchema Uri() => new();

    /// <summary>Starts a schema for a set, applying <paramref name="element"/> to each member.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="element">The schema every member must satisfy.</param>
    /// <remarks>
    /// Binds a <see cref="HashSet{T}"/>, an <see cref="IReadOnlySet{T}"/>, a frozen set or an
    /// immutable one. A member's failure is reported at the set's own path and not at an index,
    /// because a set has no positions and its enumeration order is not something to report against.
    /// Use <see cref="Collection"/> for a list, where an index means something and duplicates are
    /// possible.
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Set(Theo.String().Trim().MaxLength(24)).MaxCount(10);
    /// </code>
    /// </example>
    public static SetSchema<TElement> Set<TElement>(Schema<TElement, TElement> element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return new SetSchema<TElement>(element);
    }

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
    /// Starts a schema for a closed hierarchy, dispatching on the value's own type.
    /// </summary>
    /// <typeparam name="TBase">The base type or interface every value belongs to.</typeparam>
    /// <remarks>
    /// <para>
    /// A discriminated union. Zod's version reads a literal property, because TypeScript erased the
    /// type and so the discriminator has to be data. In C# the discriminator is the type itself:
    /// <c>System.Text.Json</c> already chose which subtype to construct before any schema ran, so the
    /// branch is picked by a type test. Faster than trying each alternative, and the error names the
    /// branch — "PixPayment: the key is missing" rather than "not any of the three".
    /// </para>
    /// <para>
    /// Choose between the three tools by what actually differs. Different <em>types</em>: this. The
    /// same type in different <em>shapes</em>, such as an identifier that may be an address or a phone
    /// number: <see cref="OneOf"/>. One flat class with a <c>Kind</c> property and rules that depend
    /// on it: <c>When</c>, which already does that and needs nothing new.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Subtypes&lt;Payment&gt;()
    ///     .Case(Theo.Object&lt;PixPayment&gt;().Field(x =&gt; x.Key, Theo.String().NotEmpty()))
    ///     .Case(Theo.Object&lt;CardPayment&gt;().Field(x =&gt; x.Number, Theo.String().Length(16)));
    /// </code>
    /// </example>
    public static SubtypesSchema<TBase> Subtypes<TBase>()
        where TBase : class => new();

    /// <summary>Requires one exact value.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The only value this schema accepts.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// The building block of a discriminated union, and useful on its own for a protocol version, a
    /// fixed currency, an agreement a caller has to state rather than assume.
    /// </para>
    /// <para>
    /// Equality is <see cref="EqualityComparer{T}.Default"/>, which for a <see cref="string"/> means
    /// ordinal and case-sensitive. There is deliberately no comparer parameter: normalizing and then
    /// comparing exactly is already how this library handles text, and
    /// <c>ToLowerInvariant()</c> followed by a literal says in two rules what a hidden comparer would
    /// only imply.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Literal("pix");
    /// Theo.Literal(2, "This endpoint only speaks version 2.");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> was null.</exception>
    public static Schema<T> Literal<T>(T value, string? message = null)
    {
        // Not ArgumentNullException.ThrowIfNull, which takes an object and would box a value type on
        // every call. For a value type this comparison is a compile-time constant the JIT removes.
        if (value is null)
        {
            throw new ArgumentNullException(
                nameof(value),
                "A literal needs a value. To accept null, use AllowNull.");
        }

        return new LiteralSchema<T>(value, message);
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
