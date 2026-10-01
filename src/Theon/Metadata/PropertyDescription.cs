namespace Theon.Metadata;

/// <summary>One property of an object, as it appears in a description.</summary>
/// <remarks>
/// A plain class rather than a record, deliberately. A record's structural equality would compare
/// <see cref="Schema"/> by reference, so two properties that describe the same thing would come back
/// unequal — an equality that is worse than none, because a caller would reasonably expect it to work.
/// </remarks>
public sealed class PropertyDescription
{
    /// <summary>Initializes a new instance of the <see cref="PropertyDescription"/> class.</summary>
    /// <param name="name">The property's name, as it appears in a document.</param>
    /// <param name="schema">What the property's value has to satisfy.</param>
    /// <param name="isRequired">Whether the property refuses <see langword="null"/>.</param>
    public PropertyDescription(string name, SchemaDescription schema, bool isRequired)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(schema);

        Name = name;
        Schema = schema;
        IsRequired = isRequired;
    }

    /// <summary>Gets the property's name, as it appears in a document.</summary>
    public string Name { get; }

    /// <summary>Gets the description of what the property's value has to satisfy.</summary>
    public SchemaDescription Schema { get; }

    /// <summary>Gets a value indicating whether the property refuses <see langword="null"/>.</summary>
    /// <remarks>
    /// Derived rather than declared. This library never checks whether a key was present — the type
    /// system settled that before parsing began — so the only thing "required" can honestly mean in a
    /// generated document is that the schema refuses null.
    /// </remarks>
    public bool IsRequired { get; }
}
