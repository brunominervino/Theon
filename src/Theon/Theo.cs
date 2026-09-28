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
}
