namespace Theon.Metadata;

/// <summary>
/// The reified structure of a schema: everything a generated document needs to know about it.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that generating a document does not have to ask what kind of schema it is holding. A
/// switch over schema types is what this library refuses in shared code, and a document generator is
/// where that temptation is strongest. Instead each schema answers with one of these, each rule
/// decorates it, and whatever writes the document switches over a data model rather than over schema
/// identity.
/// </para>
/// <para>
/// Immutable. Build one with an object initializer, and build a variant of one with the copy
/// constructor — which is what a wrapper schema does when it has one thing to change about the schema
/// it wraps:
/// </para>
/// <code>
/// public override SchemaDescription Describe(DescriptionContext context) =>
///     new(context.Describe(inner)) { AllowsNull = true };
/// </code>
/// <para>
/// Describing allocates freely. A description is produced when a document is generated, which happens
/// at start-up or in a tool and never on a parse, so the allocation rules that govern the rest of this
/// library do not apply here and are deliberately not applied.
/// </para>
/// </remarks>
/// <seealso cref="Schema{TInput, TOutput}.Describe"/>
public sealed class SchemaDescription
{
    /// <summary>Initializes a new, empty instance of the <see cref="SchemaDescription"/> class.</summary>
    /// <remarks>
    /// An empty description constrains nothing, which is the honest answer for a schema that has
    /// nothing to say about itself.
    /// </remarks>
    public SchemaDescription()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaDescription"/> class as a copy of another.
    /// </summary>
    /// <param name="other">The description to copy.</param>
    /// <remarks>
    /// For changing one thing about a description something else produced, which is what every wrapper
    /// schema does. The children are shared rather than copied, which is safe because they are
    /// immutable too.
    /// </remarks>
    public SchemaDescription(SchemaDescription other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Kind = other.Kind;
        Reference = other.Reference;
        Format = other.Format;
        Pattern = other.Pattern;
        ContentMediaType = other.ContentMediaType;
        MinLength = other.MinLength;
        MaxLength = other.MaxLength;
        Minimum = other.Minimum;
        Maximum = other.Maximum;
        ExclusiveMinimum = other.ExclusiveMinimum;
        ExclusiveMaximum = other.ExclusiveMaximum;
        MultipleOf = other.MultipleOf;
        MinItems = other.MinItems;
        MaxItems = other.MaxItems;
        UniqueItems = other.UniqueItems;
        AllowsNull = other.AllowsNull;
        Items = other.Items;
        AdditionalProperties = other.AdditionalProperties;
        Properties = other.Properties;
        AnyOf = other.AnyOf;
        AllOf = other.AllOf;
        AllowedValues = other.AllowedValues;
        ConstantValue = other.ConstantValue;
        HasConstantValue = other.HasConstantValue;
        Title = other.Title;
        Description = other.Description;
        Example = other.Example;
        IsDeprecated = other.IsDeprecated;
        DefaultValue = other.DefaultValue;
        HasDefaultValue = other.HasDefaultValue;
        Unrepresentable = other.Unrepresentable;
    }

    /// <summary>Gets what kind of value the schema accepts.</summary>
    public SchemaKind Kind { get; init; }

    /// <summary>Gets the reference this node stands in for, if it stands in for one.</summary>
    /// <remarks>
    /// Set instead of everything else when the schema is described elsewhere in the document, which is
    /// how a recursive schema is written down without recursing for ever. A generator that sees this
    /// should emit a reference and nothing beside it but annotations.
    /// </remarks>
    public string? Reference { get; init; }

    /// <summary>Gets the name of the format the value must match, such as <c>email</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Gets the regular expression the value must match.</summary>
    public string? Pattern { get; init; }

    /// <summary>Gets the media type of the content the value carries.</summary>
    /// <remarks>
    /// For a value that is not text but travels where text would — an uploaded file in a multipart
    /// request, say. Distinct from <see cref="Format"/>, which names a shape the text itself has.
    /// </remarks>
    public string? ContentMediaType { get; init; }

    /// <summary>Gets the shortest the text may be, in Unicode code points.</summary>
    public int? MinLength { get; init; }

    /// <summary>Gets the longest the text may be, in Unicode code points.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets the lower bound on the value.</summary>
    /// <remarks>
    /// Boxed, because a bound preserves the caller's exact numeric type rather than flattening
    /// everything to <see cref="double"/> and losing <see cref="decimal"/> and <see cref="long"/>
    /// precision.
    /// </remarks>
    public object? Minimum { get; init; }

    /// <summary>Gets the upper bound on the value.</summary>
    /// <inheritdoc cref="Minimum" path="/remarks"/>
    public object? Maximum { get; init; }

    /// <summary>Gets a value indicating whether <see cref="Minimum"/> is itself disallowed.</summary>
    public bool ExclusiveMinimum { get; init; }

    /// <summary>Gets a value indicating whether <see cref="Maximum"/> is itself disallowed.</summary>
    public bool ExclusiveMaximum { get; init; }

    /// <summary>Gets the divisor the value must be an exact multiple of.</summary>
    /// <inheritdoc cref="Minimum" path="/remarks"/>
    public object? MultipleOf { get; init; }

    /// <summary>Gets the fewest entries the value may have.</summary>
    public int? MinItems { get; init; }

    /// <summary>Gets the most entries the value may have.</summary>
    public int? MaxItems { get; init; }

    /// <summary>Gets a value indicating whether the entries must all differ.</summary>
    public bool UniqueItems { get; init; }

    /// <summary>Gets a value indicating whether <see langword="null"/> is accepted.</summary>
    public bool AllowsNull { get; init; }

    /// <summary>Gets the description every entry of a list must satisfy.</summary>
    public SchemaDescription? Items { get; init; }

    /// <summary>Gets the description every value of a map must satisfy.</summary>
    public SchemaDescription? AdditionalProperties { get; init; }

    /// <summary>Gets the named properties of an object.</summary>
    public IReadOnlyList<PropertyDescription>? Properties { get; init; }

    /// <summary>Gets the alternatives the value must satisfy at least one of.</summary>
    public IReadOnlyList<SchemaDescription>? AnyOf { get; init; }

    /// <summary>Gets the descriptions the value must satisfy all of.</summary>
    public IReadOnlyList<SchemaDescription>? AllOf { get; init; }

    /// <summary>Gets the values the value must be one of.</summary>
    public IReadOnlyList<object?>? AllowedValues { get; init; }

    /// <summary>Gets the single value the schema accepts, when it accepts only one.</summary>
    /// <remarks>
    /// Read it with <see cref="HasConstantValue"/>, since <see langword="null"/> is a value a schema
    /// can legitimately require.
    /// </remarks>
    public object? ConstantValue { get; init; }

    /// <summary>Gets a value indicating whether <see cref="ConstantValue"/> was set.</summary>
    public bool HasConstantValue { get; init; }

    /// <summary>Gets a short name for the value.</summary>
    public string? Title { get; init; }

    /// <summary>Gets a sentence or two about what the value means.</summary>
    public string? Description { get; init; }

    /// <summary>Gets a value worth showing a reader.</summary>
    public object? Example { get; init; }

    /// <summary>Gets a value indicating whether callers should stop using the value.</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>Gets the value produced when the input is <see langword="null"/>.</summary>
    /// <remarks>
    /// Read it with <see cref="HasDefaultValue"/>. A default is an annotation and not an assertion: it
    /// tells a reader what happens without claiming anything will enforce it.
    /// </remarks>
    public object? DefaultValue { get; init; }

    /// <summary>Gets a value indicating whether <see cref="DefaultValue"/> was set.</summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>Gets the rules this description could not express, named after each rule.</summary>
    /// <remarks>
    /// Kept rather than discarded, so that a caller can ask to be told. A document silently missing a
    /// constraint is the failure mode this list exists to make visible: a document that omits a rule is
    /// incomplete, and one that states a rule nothing enforces is wrong.
    /// </remarks>
    public IReadOnlyList<string>? Unrepresentable { get; init; }
}
