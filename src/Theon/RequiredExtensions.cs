using Theon.Schemas;

namespace Theon;

/// <summary>Requires a value for a nullable value type.</summary>
/// <remarks>
/// Split across two static classes, like <c>AllowNull</c>, so the compiler picks by constraint and
/// both spellings read the same at the call site.
/// </remarks>
public static class ValueSchemaRequiredExtensions
{
    /// <summary>
    /// Produces a schema of the nullable type that refuses <see langword="null"/> and otherwise
    /// applies this schema.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="schema">The schema to apply once a value is present.</param>
    /// <param name="message">A message that replaces the default when the value is absent.</param>
    /// <example>
    /// <code>
    /// // CompletedAt is a DateTime?, and here it must have a value.
    /// .Field(x => x.CompletedAt, Theo.DateTime().RequireUtc().Required("A date is required."))
    /// </code>
    /// </example>
    public static Schema<T?> Required<T>(this Schema<T> schema, string? message = null)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new RequiredValueSchema<T>(schema, message);
    }
}

/// <summary>Requires a value for a nullable reference type.</summary>
/// <inheritdoc cref="ValueSchemaRequiredExtensions" path="/remarks"/>
public static class ReferenceSchemaRequiredExtensions
{
    /// <summary>
    /// Produces a schema of the nullable type that refuses <see langword="null"/> and otherwise
    /// applies this schema.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="schema">The schema to apply once a value is present.</param>
    /// <param name="message">A message that replaces the default when the value is absent.</param>
    public static Schema<T?> Required<T>(this Schema<T> schema, string? message = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new RequiredReferenceSchema<T>(schema, message);
    }
}
