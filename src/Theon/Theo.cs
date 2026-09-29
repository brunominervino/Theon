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
    /// <typeparam name="T">The object type to validate.</typeparam>
    public static ObjectSchema<T> Object<T>()
        where T : class => new();

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
}
