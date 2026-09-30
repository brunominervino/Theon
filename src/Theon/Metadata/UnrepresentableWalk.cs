using System.Globalization;

namespace Theon.Metadata;

// Gathers the rules a document had to leave out, with the path each one was on.
//
// The names alone would not be actionable. "A refinement could not be expressed" is not something
// anyone can act on in a schema with forty fields; "Address.ZipCode: RefineCheck" is.
internal static class UnrepresentableWalk
{
    internal static List<string> Collect(
        SchemaDescription root,
        IReadOnlyDictionary<string, SchemaDescription> definitions)
    {
        var found = new List<string>();

        Walk(root, string.Empty, found);

        foreach (var (name, description) in definitions)
        {
            Walk(description, "$defs/" + name, found);
        }

        return found;
    }

    private static void Walk(SchemaDescription description, string path, List<string> found)
    {
        // A reference has no children of its own; whatever it points at is walked once, where it is
        // defined. That is also what stops a recursive schema from walking for ever.
        if (description.Reference is not null)
        {
            return;
        }

        var where = path.Length == 0 ? "(root)" : path;

        if (description.Unrepresentable is { Count: > 0 } rules)
        {
            foreach (var rule in rules)
            {
                found.Add(string.Create(CultureInfo.InvariantCulture, $"{where}: {rule}"));
            }
        }

        ReportDroppedKeywords(description, where, found);

        if (description.Properties is { } properties)
        {
            foreach (var property in properties)
            {
                Walk(property.Schema, Join(path, property.Name), found);
            }
        }

        if (description.Items is { } items)
        {
            Walk(items, path + "[]", found);
        }

        if (description.AdditionalProperties is { } additional)
        {
            Walk(additional, Join(path, "*"), found);
        }

        // A branch of a union or an intersection sits at the same position as the value itself, so it
        // adds nothing to the path.
        WalkAll(description.AnyOf, path, found);
        WalkAll(description.AllOf, path, found);
    }

    // A rule that described itself into a keyword the writer then has to drop is the quietest way to
    // lose a constraint: the rule believed it had been recorded, and the document does not have it. A
    // temporal bound is the case that happens -- a date is a string, and a range is numeric.
    private static void ReportDroppedKeywords(
        SchemaDescription description,
        string where,
        List<string> found)
    {
        if (!Representable.Range(description.Kind))
        {
            if (description.Minimum is not null)
            {
                found.Add($"{where}: a minimum bound, which has no keyword for a {Describe(description.Kind)}");
            }

            if (description.Maximum is not null)
            {
                found.Add($"{where}: a maximum bound, which has no keyword for a {Describe(description.Kind)}");
            }

            if (description.MultipleOf is not null)
            {
                found.Add($"{where}: a divisor, which has no keyword for a {Describe(description.Kind)}");
            }
        }

        if (!Representable.Length(description.Kind) &&
            (description.MinLength is not null || description.MaxLength is not null))
        {
            found.Add($"{where}: a length bound, which has no keyword for a {Describe(description.Kind)}");
        }

        if (!Representable.Count(description.Kind) &&
            (description.MinItems is not null || description.MaxItems is not null))
        {
            found.Add($"{where}: a count bound, which has no keyword for a {Describe(description.Kind)}");
        }
    }

    private static string Describe(SchemaKind kind) => kind switch
    {
        SchemaKind.String => "string",
        SchemaKind.Integer => "integer",
        SchemaKind.Number => "number",
        SchemaKind.Boolean => "boolean",
        SchemaKind.Object => "object",
        SchemaKind.Array => "array",
        SchemaKind.Map => "map",
        _ => "value of unknown type",
    };

    private static void WalkAll(List<SchemaDescription>? branches, string path, List<string> found)
    {
        if (branches is null)
        {
            return;
        }

        foreach (var branch in branches)
        {
            Walk(branch, path, found);
        }
    }

    private static string Join(string path, string name) =>
        path.Length == 0 ? name : path + "." + name;
}
