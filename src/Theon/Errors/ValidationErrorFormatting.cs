namespace Theon.Errors;

/// <summary>
/// Reshapes a flat list of errors into the forms a caller actually renders.
/// </summary>
public static class ValidationErrorFormatting
{
    /// <summary>Groups errors by the field they belong to.</summary>
    /// <param name="errors">The errors to group.</param>
    /// <example>
    /// <code>
    /// var flat = result.Errors.Flatten();
    /// return TypedResults.ValidationProblem(
    ///     flat.ToDictionary().ToDictionary(p => p.Key, p => p.Value.ToArray()));
    /// </code>
    /// </example>
    public static FlattenedErrors Flatten(this IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        List<string>? root = null;
        Dictionary<string, List<string>>? fields = null;

        foreach (var error in errors)
        {
            if (error.Path.IsRoot)
            {
                (root ??= []).Add(error.Message);
                continue;
            }

            fields ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var key = error.Path.ToString();

            if (!fields.TryGetValue(key, out var messages))
            {
                messages = [];
                fields[key] = messages;
            }

            messages.Add(error.Message);
        }

        return new FlattenedErrors(
            root is null ? Array.Empty<string>() : root,
            fields is null
                ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                : fields.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal));
    }

    /// <summary>Arranges errors to mirror the shape of the value that produced them.</summary>
    /// <param name="errors">The errors to arrange.</param>
    public static ValidationErrorTree ToTree(this IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var root = new TreeBuilder();

        foreach (var error in errors)
        {
            var node = root;
            foreach (var segment in error.Path.Segments)
            {
                node = segment.IsIndex
                    ? node.Item(segment.ElementIndex)
                    : node.Property(segment.Name!);
            }

            node.Messages.Add(error.Message);
        }

        return root.Build();
    }

    private sealed class TreeBuilder
    {
        internal List<string> Messages { get; } = [];

        private Dictionary<string, TreeBuilder>? _properties;
        private Dictionary<int, TreeBuilder>? _items;

        internal TreeBuilder Property(string name)
        {
            _properties ??= new Dictionary<string, TreeBuilder>(StringComparer.Ordinal);
            if (!_properties.TryGetValue(name, out var child))
            {
                child = new TreeBuilder();
                _properties[name] = child;
            }

            return child;
        }

        internal TreeBuilder Item(int index)
        {
            _items ??= [];
            if (!_items.TryGetValue(index, out var child))
            {
                child = new TreeBuilder();
                _items[index] = child;
            }

            return child;
        }

        internal ValidationErrorTree Build() => new(
            Messages,
            _properties is null
                ? new Dictionary<string, ValidationErrorTree>(StringComparer.Ordinal)
                : _properties.ToDictionary(p => p.Key, p => p.Value.Build(), StringComparer.Ordinal),
            _items is null
                ? new Dictionary<int, ValidationErrorTree>()
                : _items.ToDictionary(p => p.Key, p => p.Value.Build()));
    }
}
