using Theon.Schemas;

namespace Theon;

/// <summary>
/// Makes an <see cref="ObjectSchema{T}"/> over a reference type accept <see langword="null"/>.
/// </summary>
/// <remarks>
/// This lives in an extension method, and in its own class, for a specific reason: a member of
/// <see cref="ObjectSchema{T}"/> cannot add a constraint to the type parameter the class itself
/// declared, and the reference and value cases need genuinely different wrappers — one is an
/// annotation over the same runtime type, the other is a <see cref="Nullable{T}"/>. Two extension
/// methods in two static classes let the compiler pick by constraint, so both read as
/// <c>AllowNull()</c> at the call site.
/// </remarks>
public static class ReferenceObjectSchemaExtensions
{
    /// <summary>Accepts <see langword="null"/> in addition to everything the schema accepts.</summary>
    /// <typeparam name="T">The reference type being validated.</typeparam>
    /// <param name="schema">The schema to widen.</param>
    public static Schema<T?> AllowNull<T>(this ObjectSchema<T> schema)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new NullableReferenceSchema<T>(schema);
    }
}

/// <summary>
/// Makes an <see cref="ObjectSchema{T}"/> over a value type accept <see langword="null"/>.
/// </summary>
/// <inheritdoc cref="ReferenceObjectSchemaExtensions" path="/remarks"/>
public static class ValueObjectSchemaExtensions
{
    /// <summary>Accepts <see langword="null"/> in addition to everything the schema accepts.</summary>
    /// <typeparam name="T">The value type being validated.</typeparam>
    /// <param name="schema">The schema to widen.</param>
    public static Schema<T?> AllowNull<T>(this ObjectSchema<T> schema)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new NullableValueSchema<T>(schema);
    }
}
