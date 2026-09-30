namespace Theon.Errors;

/// <summary>
/// Validation errors arranged to mirror the shape of the value that produced them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FlattenedErrors"/> answers "what is wrong with this field", which is what a flat form
/// needs. This answers "what is wrong inside this part of the object", which is what a nested form
/// or a recursive renderer needs — each component receives the subtree for the value it is drawing
/// and never has to parse a path string to find out whether anything below it failed.
/// </para>
/// <para>
/// Elements are keyed by index rather than held in a list, because failures in a collection are
/// usually sparse: one bad row in two hundred should cost one entry, not two hundred.
/// </para>
/// </remarks>
public sealed class ValidationErrorTree
{
    internal ValidationErrorTree(
        IReadOnlyList<string> errors,
        IReadOnlyDictionary<string, ValidationErrorTree> properties,
        IReadOnlyDictionary<int, ValidationErrorTree> items)
    {
        Errors = errors;
        Properties = properties;
        Items = items;
    }

    /// <summary>Gets the messages that belong to this node itself.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Gets the subtrees for named properties that contain a failure.</summary>
    public IReadOnlyDictionary<string, ValidationErrorTree> Properties { get; }

    /// <summary>Gets the subtrees for collection elements that contain a failure, keyed by index.</summary>
    public IReadOnlyDictionary<int, ValidationErrorTree> Items { get; }

    /// <summary>Gets a value indicating whether this node and everything below it is clean.</summary>
    public bool IsEmpty => Errors.Count == 0 && Properties.Count == 0 && Items.Count == 0;
}
