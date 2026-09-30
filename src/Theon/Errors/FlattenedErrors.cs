namespace Theon.Errors;

/// <summary>
/// Validation errors grouped by the field they belong to.
/// </summary>
/// <remarks>
/// <para>
/// The shape a form wants, and the shape an API response wants. A caller that has to build either
/// one from the flat list ends up writing the same grouping loop every time, and getting the
/// root-level case subtly wrong.
/// </para>
/// <para>
/// Keys are rendered paths — <c>Address.ZipCode</c>, <c>Recipients[1]</c> — in declaration order,
/// and within a field the messages keep the order the rules ran in.
/// </para>
/// </remarks>
public sealed class FlattenedErrors
{
    internal FlattenedErrors(
        IReadOnlyList<string> rootErrors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> fieldErrors)
    {
        RootErrors = rootErrors;
        FieldErrors = fieldErrors;
    }

    /// <summary>Gets the messages that belong to the value as a whole rather than to a field.</summary>
    /// <remarks>
    /// These come from object-level rules, and from a failure of the value itself, such as being
    /// <see langword="null"/>. A form usually shows them above the fields.
    /// </remarks>
    public IReadOnlyList<string> RootErrors { get; }

    /// <summary>Gets the messages for each field that has any, keyed by its rendered path.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }

    /// <summary>Gets a value indicating whether anything failed at all.</summary>
    public bool IsEmpty => RootErrors.Count == 0 && FieldErrors.Count == 0;

    /// <summary>
    /// Renders as a dictionary of field to messages, with the root messages under
    /// <paramref name="rootKey"/>.
    /// </summary>
    /// <param name="rootKey">
    /// The key to file the root messages under. Defaults to the empty string, which is the
    /// convention ASP.NET Core uses for model-level errors.
    /// </param>
    /// <remarks>
    /// Offered because that one shape is what most HTTP responses want, and writing the merge by
    /// hand is exactly the kind of small thing every caller would do slightly differently.
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ToDictionary(string rootKey = "")
    {
        if (RootErrors.Count == 0)
        {
            return FieldErrors;
        }

        var merged = new Dictionary<string, IReadOnlyList<string>>(FieldErrors.Count + 1, StringComparer.Ordinal)
        {
            [rootKey] = RootErrors,
        };

        foreach (var pair in FieldErrors)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }
}
