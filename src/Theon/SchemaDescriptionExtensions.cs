using Theon.Metadata;

namespace Theon;

/// <summary>Describes a schema, for writing a generator of your own.</summary>
public static class SchemaDescriptionExtensions
{
    /// <summary>Describes this schema and everything it contains.</summary>
    /// <typeparam name="TInput">The type the schema accepts.</typeparam>
    /// <typeparam name="TOutput">The type the schema produces.</typeparam>
    /// <param name="schema">The schema to describe.</param>
    /// <param name="direction">
    /// Which side of the schema to describe. <see cref="DescriptionDirection.Input"/> for what a caller
    /// sends, <see cref="DescriptionDirection.Output"/> for what the schema produces.
    /// </param>
    /// <param name="referencePrefix">
    /// What a reference to a repeated schema is written with. <c>#/$defs/</c> by default, which is JSON
    /// Schema's spelling; a generator for another format should pass whatever its own references look
    /// like, or an empty string to get the bare names.
    /// </param>
    /// <returns>The description, with whatever repeated kept beside it.</returns>
    /// <remarks>
    /// <para>
    /// For writing a generator this library does not ship — protobuf, Avro, a form, a documentation
    /// page. <c>ToJsonSchema</c> is one such generator and is built on exactly this and nothing more.
    /// </para>
    /// <para>
    /// A schema that repeats within the result, which is what a recursive schema does, is described once
    /// and referred to by name from everywhere it appears. A generator has to handle
    /// <see cref="SchemaDescription.Reference"/> for that reason, and handling it is also what keeps the
    /// result finite.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var described = schema.Describe();
    ///
    /// Write(described.Root);
    ///
    /// foreach (var (name, definition) in described.Definitions)
    /// {
    ///     WriteNamed(name, definition);
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// The schema nested deeper than the walk will follow, which a recursive schema built from a factory
    /// that returns a new instance each time will do.
    /// </exception>
    public static SchemaDescriptionSet Describe<TInput, TOutput>(
        this Schema<TInput, TOutput> schema,
        DescriptionDirection direction = DescriptionDirection.Input,
        string referencePrefix = "#/$defs/")
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(referencePrefix);

        var context = new DescriptionContext(referencePrefix, direction);

        return new SchemaDescriptionSet(context.Describe(schema), context.Definitions);
    }
}
