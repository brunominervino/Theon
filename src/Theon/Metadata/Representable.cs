namespace Theon.Metadata;

// Which keywords apply to which kind of value.
//
// One place, because two things need the answer and they have to agree. The writer uses it to decide
// what to emit, and the same walk uses it to decide what was lost. If they disagreed, a bound would be
// dropped from the document and the caller who asked to be told about exactly that would hear nothing
// -- which is the bug this file exists to have fixed. They are now one walk as well as one answer,
// which is belt and braces on the same hazard.
internal static class Representable
{
    // minimum, maximum, exclusiveMinimum, exclusiveMaximum and multipleOf are numeric keywords. A
    // validator ignores them on a string, which is what a date and a duration both are here.
    internal static bool Range(SchemaKind kind) => kind is SchemaKind.Integer or SchemaKind.Number;

    internal static bool Length(SchemaKind kind) => kind is SchemaKind.String;

    internal static bool Count(SchemaKind kind) => kind is SchemaKind.Array or SchemaKind.Map;
}
