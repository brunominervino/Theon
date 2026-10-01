namespace Theon.Metadata;

/// <summary>
/// One schema described, with the schemas it refers to kept beside it.
/// </summary>
/// <remarks>
/// <para>
/// The entry point for writing a generator of your own — for protobuf, for Avro, for rendering a form,
/// for whatever a document means in your project. <c>ToJsonSchema</c> is one such generator and has no
/// more access to a schema than this gives you.
/// </para>
/// <para>
/// The two are apart because a schema that repeats within a description — which is what a recursive
/// schema does — is described once, given a name, and referred to by that name everywhere else. A
/// generator writes the definitions wherever its own format keeps shared definitions.
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
public sealed class SchemaDescriptionSet
{
    internal SchemaDescriptionSet(
        SchemaDescription root,
        IReadOnlyDictionary<string, SchemaDescription> definitions)
    {
        Root = root;
        Definitions = definitions;
    }

    /// <summary>Gets the schema itself, which may be a reference into <see cref="Definitions"/>.</summary>
    public SchemaDescription Root { get; }

    /// <summary>Gets the descriptions referred to by name, by the name each reference uses.</summary>
    /// <remarks>
    /// Empty unless something repeated, which in practice means unless a schema referred to itself.
    /// </remarks>
    public IReadOnlyDictionary<string, SchemaDescription> Definitions { get; }
}
