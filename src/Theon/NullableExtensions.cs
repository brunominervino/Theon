using Theon.Schemas;

namespace Theon;

/// <summary>
/// Makes a schema over a reference type accept <see langword="null"/>.
/// </summary>
/// <remarks>
/// Extension methods, in two static classes, for a specific reason: a member cannot add a
/// constraint to the type parameter its own class declared, and the reference and value cases need
/// genuinely different wrappers — one is an annotation over the same runtime type, the other is a
/// <see cref="Nullable{T}"/>. Two methods let the compiler pick by constraint, so both read as
/// <c>AllowNull()</c> at the call site.
/// </remarks>
public static class ReferenceSchemaNullableExtensions
{
    /// <summary>Accepts <see langword="null"/> in addition to everything the schema accepts.</summary>
    /// <typeparam name="T">The reference type being validated.</typeparam>
    /// <param name="schema">The schema to widen.</param>
    /// <remarks>
    /// Declared over <see cref="Schema{T}"/> rather than any one schema type, so it reaches
    /// composed schemas too — the result of a <c>Transform</c> or a <c>RefineAsync</c> is widened
    /// the same way as a plain one.
    /// </remarks>
    public static Schema<T?> AllowNull<T>(this Schema<T> schema)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new NullableReferenceSchema<T>(schema);
    }
}

/// <summary>
/// Makes a schema over a value type accept <see langword="null"/>.
/// </summary>
/// <inheritdoc cref="ReferenceSchemaNullableExtensions" path="/remarks"/>
public static class ValueSchemaNullableExtensions
{
    /// <summary>Accepts <see langword="null"/> in addition to everything the schema accepts.</summary>
    /// <typeparam name="T">The value type being validated.</typeparam>
    /// <param name="schema">The schema to widen.</param>
    /// <inheritdoc cref="ReferenceSchemaNullableExtensions.AllowNull{T}(Schema{T})" path="/remarks"/>
    public static Schema<T?> AllowNull<T>(this Schema<T> schema)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new NullableValueSchema<T>(schema);
    }
}
