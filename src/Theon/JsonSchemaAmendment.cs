using System.Text.Json.Nodes;
using Theon.Metadata;

namespace Theon;

/// <summary>
/// Amends one node of a generated document, after it has been written.
/// </summary>
/// <param name="node">The node, with the path it is at and what it could not express.</param>
/// <remarks>
/// <para>
/// Called once for every node in the document, children before their parents, so an amendment on an
/// object sees its fields as they will be read.
/// </para>
/// <para>
/// This is the escape hatch for the one thing this library will not do on a caller's behalf: invent a
/// keyword for a rule whose meaning only the caller knows. A <c>Refine</c> that checks a postcode
/// against a national format has a <c>pattern</c> that would express it, and nothing in this library
/// can discover what that pattern is.
/// </para>
/// </remarks>
public delegate void JsonSchemaAmendment(JsonSchemaNode node);

/// <summary>
/// One node of a generated document, offered to an amendment.
/// </summary>
/// <remarks>
/// The schema that produced the node is deliberately not here. It would have to arrive as
/// <see cref="object"/>, since its type parameters are not knowable at this signature, and an
/// <see cref="object"/> a caller has to pattern-match is exactly the branching on schema identity
/// this library refuses in its own code — it would also fix the name of every internal wrapper type
/// as a compatibility obligation for ever. The path says where the node is, and
/// <see cref="Unrepresentable"/> says what it could not say, which is enough to aim at.
/// </remarks>
public sealed class JsonSchemaNode
{
    internal JsonSchemaNode(
        string path,
        JsonObject json,
        SchemaDescription description,
        IReadOnlyList<string> unrepresentable)
    {
        Path = path;
        Json = json;
        Description = description;
        Unrepresentable = unrepresentable;
    }

    /// <summary>Gets where in the schema this node is.</summary>
    /// <remarks>
    /// <para>
    /// Empty at the root. A property is its name under its parent (<c>Address.ZipCode</c>), a
    /// collection's element is its parent followed by <c>[]</c> (<c>Recipients[]</c>), a map's value is
    /// its parent followed by <c>*</c>, and a schema promoted into the definitions is
    /// <c>$defs/</c> and its name.
    /// </para>
    /// <para>
    /// A branch of an <c>anyOf</c> or an <c>allOf</c> shares its parent's path, because it sits at the
    /// same position as the value itself. Telling two branches apart means looking at
    /// <see cref="Json"/>.
    /// </para>
    /// </remarks>
    public string Path { get; }

    /// <summary>Gets the node as written, to be changed in place.</summary>
    /// <remarks>
    /// Adding, replacing and removing keys are all allowed. The document has not been handed to anyone
    /// yet, so nothing here is shared.
    /// </remarks>
    public JsonObject Json { get; }

    /// <summary>Gets what the schema said about itself, which this node was written from.</summary>
    /// <remarks>
    /// The structured facts, where <see cref="Json"/> is one rendering of them. Read it to recover
    /// something the dialect had nowhere to put — a temporal bound, a format name — and write it
    /// wherever the surrounding document wants it.
    /// </remarks>
    public SchemaDescription Description { get; }

    /// <summary>Gets the rules of this node that the document could not state.</summary>
    /// <remarks>
    /// Either a rule that reported itself unrepresentable, named after the rule — <c>RefineCheck</c>,
    /// <c>TrimCheck</c> — or a keyword this dialect has nowhere to put, such as a minimum bound on a
    /// value that travels as a string. Empty for a node that lost nothing, and always empty for a
    /// <c>$ref</c>, which has no rules of its own.
    /// </remarks>
    public IReadOnlyList<string> Unrepresentable { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this amendment has expressed this node's rules itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set it when what you wrote into <see cref="Json"/> says what
    /// <see cref="Unrepresentable"/> listed, and <see cref="UnrepresentablePolicy.Throw"/> will stop
    /// reporting this node. Leave it alone and the node is still reported, however much you changed:
    /// the writer cannot tell a <c>pattern</c> that expresses a refinement from one that does not, and
    /// a document believed to be complete that quietly is not is the failure that policy exists to
    /// prevent.
    /// </para>
    /// <para>
    /// This covers the node and nothing else. A document with two refinements in it and an amendment
    /// that understands one of them is still told about the other.
    /// </para>
    /// </remarks>
    public bool Expressed { get; set; }
}
