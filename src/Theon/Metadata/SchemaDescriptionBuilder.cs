namespace Theon.Metadata;

// Builds a SchemaDescription a rule at a time.
//
// The public description is immutable, which is right for something handed to a generator that has no
// business changing it. Building one is the opposite problem: a leaf schema starts with a kind and then
// lets each of its rules decorate the result, and there are twenty-one rules that do so.
//
// So the mutable half lives here and stays internal, which is what makes the public half free. A custom
// schema from another assembly does not need this: it has no Check of ours to run, so it writes its
// description with an object initializer and never sees a builder.
internal sealed class SchemaDescriptionBuilder
{
    internal SchemaKind Kind { get; set; }

    internal string? Format { get; set; }

    internal string? Pattern { get; set; }

    internal string? ContentMediaType { get; set; }

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

    internal object? DefaultValue { get; set; }

    internal bool HasDefaultValue { get; set; }

    private List<string>? _unrepresentable;

    // Records that a rule has no keyword to map to, so that a caller who asked to be told can be.
    internal void CannotRepresent(string rule) => (_unrepresentable ??= []).Add(rule);

    internal SchemaDescription ToDescription() => new()
    {
        Kind = Kind,
        Format = Format,
        Pattern = Pattern,
        ContentMediaType = ContentMediaType,
        MinLength = MinLength,
        MaxLength = MaxLength,
        Minimum = Minimum,
        Maximum = Maximum,
        ExclusiveMinimum = ExclusiveMinimum,
        ExclusiveMaximum = ExclusiveMaximum,
        MultipleOf = MultipleOf,
        MinItems = MinItems,
        MaxItems = MaxItems,
        UniqueItems = UniqueItems,
        AllowsNull = AllowsNull,
        Items = Items,
        AdditionalProperties = AdditionalProperties,
        Properties = Properties,
        AnyOf = AnyOf,
        AllOf = AllOf,
        AllowedValues = AllowedValues,
        ConstantValue = ConstantValue,
        HasConstantValue = HasConstantValue,
        DefaultValue = DefaultValue,
        HasDefaultValue = HasDefaultValue,
        Unrepresentable = _unrepresentable,
    };
}
