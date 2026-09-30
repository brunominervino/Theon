namespace Theon.Metadata;

// What kind of JSON value a schema accepts.
// Deliberately coarse. A Guid, a DateTime and an e-mail address are all strings as far as a JSON
// document is concerned, and what distinguishes them is the format, not the kind.
internal enum SchemaKind
{
    // The schema did not describe itself. A custom schema from another assembly cannot override the
    // description hook, and a document generated for one has to say "anything" rather than guess.
    Unknown,
    String,
    Integer,
    Number,
    Boolean,
    Object,
    Array,
    Map,
}

// One property of an object, as it appears in a description.
// Required is derived rather than declared: this library never checks whether a key was present —
// the type system settled that before parsing began — so the only thing "required" can honestly
// mean in a generated document is that the schema refuses null.
internal sealed record PropertyDescription(string Name, SchemaDescription Schema, bool IsRequired);

// The reified structure of a schema: everything a generated document needs to know about it.
//
// This exists so that generating a document does not have to ask what kind of schema it is holding.
// A switch over schema types is exactly what this repository refuses in shared code, and a document
// generator is where that temptation is strongest, because the TypeScript library it takes its ideas
// from is written that way. Instead each schema answers with one of these, each rule decorates it,
// and the writer switches over a data model we own and can extend without touching a parse path.
//
// Mutable, and built up in place, because a description is produced once when a document is
// generated rather than on any parse. Nothing here is shared between calls.
internal sealed class SchemaDescription
{
    internal SchemaKind Kind { get; set; }

    // Set instead of everything else when this node stands in for a schema described elsewhere in
    // the document, which is how a recursive schema is written down without recursing for ever.
    internal string? Reference { get; set; }

    internal string? Format { get; set; }

    internal string? Pattern { get; set; }

    internal int? MinLength { get; set; }

    internal int? MaxLength { get; set; }

    internal object? Minimum { get; set; }

    internal object? Maximum { get; set; }

    internal bool ExclusiveMinimum { get; set; }

    internal bool ExclusiveMaximum { get; set; }

    internal object? MultipleOf { get; set; }

    internal int? MinItems { get; set; }

    internal int? MaxItems { get; set; }

    internal bool UniqueItems { get; set; }

    internal bool AllowsNull { get; set; }

    internal SchemaDescription? Items { get; set; }

    internal SchemaDescription? AdditionalProperties { get; set; }

    internal List<PropertyDescription>? Properties { get; set; }

    internal List<SchemaDescription>? AnyOf { get; set; }

    internal List<SchemaDescription>? AllOf { get; set; }

    internal List<object?>? AllowedValues { get; set; }

    internal object? ConstantValue { get; set; }

    internal bool HasConstantValue { get; set; }

    internal string? Title { get; set; }

    internal string? Description { get; set; }

    internal object? Example { get; set; }

    internal bool IsDeprecated { get; set; }

    // The rules this description could not express, by the name of the rule that could not be
    // expressed. Kept rather than discarded so that a caller can ask to be told: a document silently
    // missing a constraint is the failure mode this list exists to make visible.
    internal List<string>? Unrepresentable { get; set; }

    internal void CannotRepresent(string rule) => (Unrepresentable ??= []).Add(rule);

    internal object? DefaultValue { get; set; }

    internal bool HasDefaultValue { get; set; }
}
