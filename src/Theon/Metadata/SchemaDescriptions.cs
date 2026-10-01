namespace Theon.Metadata;

// Small operations on a description something else produced.
//
// The description model is immutable, which is right for a value handed to a generator that has no
// business changing it, and it means a wrapper schema cannot simply add to what its inner schema said.
// Adding one rule to the list of what could not be expressed is the only such operation that happens
// more than once, so it lives here rather than being written out five times.
internal static class SchemaDescriptions
{
    internal static SchemaDescription CannotRepresent(this SchemaDescription description, string rule)
    {
        var rules = description.Unrepresentable is { } existing
            ? new List<string>(existing) { rule }
            : [rule];

        return new SchemaDescription(description) { Unrepresentable = rules };
    }
}
