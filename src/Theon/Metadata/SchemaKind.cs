namespace Theon.Metadata;

/// <summary>What kind of JSON value a schema accepts.</summary>
/// <remarks>
/// Deliberately coarse. A <see cref="System.Guid"/>, a <see cref="System.DateTime"/> and an e-mail
/// address are all strings as far as a JSON document is concerned, and what distinguishes them is the
/// format, not the kind. There are only so many kinds of JSON value, which is why this is an
/// enumeration and not an open vocabulary.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "These are the JSON type names verbatim -- string, integer, object -- which is " +
                    "what a reader of a generated document sees and what the dialect's own type " +
                    "keyword takes. Renaming them to avoid a CLR type name would mean this " +
                    "vocabulary no longer matched the thing it describes.")]
public enum SchemaKind
{
    /// <summary>The schema did not say what kind of value it accepts.</summary>
    /// <remarks>
    /// The honest answer for a schema that does not describe itself, and a document generated for one
    /// has to say "anything" rather than guess. A custom schema reaches this by not overriding
    /// <c>Describe</c>.
    /// </remarks>
    Unknown,

    /// <summary>Text.</summary>
    String,

    /// <summary>Nothing: the value may only be <see langword="null"/>.</summary>
    /// <remarks>
    /// Distinct from <see cref="Unknown"/>, which says the schema constrains nothing at all. Nothing
    /// in this library produces a schema of this kind — a C# type that can hold only null is not a
    /// type anybody writes — but a document read with <c>Theo.JsonSchema</c> may say it, and reading
    /// it as "no constraint" would accept every value where the document accepts one.
    /// </remarks>
    Null,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A number that may have a fractional part.</summary>
    Number,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>An object with named properties, each governed by its own schema.</summary>
    Object,

    /// <summary>An ordered list of values.</summary>
    Array,

    /// <summary>
    /// An object whose property names are not known in advance, all governed by one schema.
    /// </summary>
    /// <remarks>
    /// A JSON object either way; the distinction from <see cref="Object"/> is whether the keys are
    /// declared, which decides whether a bound on how many there are is spelled <c>minItems</c> or
    /// <c>minProperties</c>.
    /// </remarks>
    Map,
}
