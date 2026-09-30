using Theon.Schemas;

namespace Theon;

/// <summary>Supplies a value in place of <see langword="null"/> for a nullable value type.</summary>
/// <remarks>
/// Split across two static classes, like <c>AllowNull</c> and <c>Required</c>, so the compiler picks
/// by constraint and both spellings read the same at the call site.
/// </remarks>
public static class ValueSchemaDefaultExtensions
{
    /// <summary>
    /// Produces a schema that accepts <see langword="null"/> and answers with
    /// <paramref name="value"/>, and otherwise applies this schema.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="schema">The schema to apply once a value is present.</param>
    /// <param name="value">The value to use when none was supplied.</param>
    /// <remarks>
    /// <para>
    /// The counterpart of <see cref="ValueSchemaRequiredExtensions.Required"/>: both turn a schema of
    /// <typeparamref name="T"/> into one of <c>T?</c>, and they differ only in what happens when the
    /// value is absent. One reports; this one answers.
    /// </para>
    /// <para>
    /// This shapes the value a parse <em>produces</em>, so it belongs where that value is read: a
    /// top-level <c>Parse</c> or <c>SafeParse</c>, or a step in a <c>Transform</c> chain. An object
    /// schema validates an instance rather than rebuilding one, and never writes to it, so a default
    /// on a field would be computed and then discarded. C# already has a better answer there — a
    /// property initializer, or the deserializer's own defaults.
    /// </para>
    /// <para>
    /// The default is not itself run through <paramref name="schema"/>. There is nothing to learn
    /// from checking a value the schema's own author wrote, and doing so would report a failure
    /// against input the caller never supplied.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // A page size that may be absent, clamped when it is not.
    /// private static readonly Schema&lt;int?, int&gt; PageSize =
    ///     Theo.Int().Min(1).Max(100).Default(20);
    ///
    /// var size = PageSize.Parse(query.PageSize); // 20 when null
    /// </code>
    /// </example>
    public static Schema<T?, T> Default<T>(this Schema<T> schema, T value)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new DefaultValueSchema<T>(schema, value);
    }
}

/// <summary>Supplies a value in place of <see langword="null"/> for a reference type.</summary>
/// <inheritdoc cref="ValueSchemaDefaultExtensions" path="/remarks"/>
public static class ReferenceSchemaDefaultExtensions
{
    /// <summary>
    /// Produces a schema that accepts <see langword="null"/> and answers with
    /// <paramref name="value"/>, and otherwise applies this schema.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="schema">The schema to apply once a value is present.</param>
    /// <param name="value">The value to use when none was supplied. Must not be null.</param>
    /// <inheritdoc cref="ValueSchemaDefaultExtensions.Default" path="/remarks"/>
    public static Schema<T?, T> Default<T>(this Schema<T> schema, T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);

        // A null default would mean "accept null and answer null", which is AllowNull spelled at
        // twice the length. Failing here says which of the two was meant.
        ArgumentNullException.ThrowIfNull(value);

        return new DefaultReferenceSchema<T>(schema, value);
    }
}
