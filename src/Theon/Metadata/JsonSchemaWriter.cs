using System.Globalization;
using System.Text.Json.Nodes;

namespace Theon.Metadata;

// Turns a description into a JSON Schema object, records what it had to leave out, and offers each
// node to the caller's amendment on the way past.
//
// This is the one place that switches on what kind of thing it is looking at, and it switches on the
// description model rather than on schema types. That is the whole point of having a description
// model: the rule against branching on schema identity protects the parse path, and a document
// generator is exactly where that rule would otherwise be broken first.
//
// Writing and reporting are one walk rather than two. They used to be separate -- a writer and an
// UnrepresentableWalk -- and each built its own paths and asked its own questions about which
// keywords applied. Two answers that had to agree is a bug waiting to be written, and the amendment
// made it worse: a node cannot be offered its own losses unless the thing writing it is the thing
// that knows them.
//
// The dialect is JSON Schema 2020-12, which is also what OpenAPI 3.1 uses, so one writer serves
// both. OpenAPI 3.0 is deliberately not supported: it predates that alignment, spells nullability
// with a keyword of its own and has no $defs, so serving it would mean a second writer to keep in
// step with this one.
internal sealed class JsonSchemaWriter(JsonSchemaAmendment? amend)
{
    internal const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    private static readonly string[] NothingLost = [];

    private readonly List<string> _lost = [];

    // The rules this document could not state, each prefixed with the path it was at. The names alone
    // would not be actionable: "a refinement could not be expressed" is not something anyone can act
    // on in a schema with forty fields, where "Address.ZipCode: RefineCheck" is.
    internal IReadOnlyList<string> Lost => _lost;

    // Writes the whole document: the dialect, the root schema, and the definitions the root and its
    // children referred to. The keys go in this order because a reader opens the document at the top,
    // and a node cannot be moved between parents afterwards to reorder them.
    //
    // The definitions are written whether or not they are going inside the document, so an amendment
    // sees every node exactly once either way.
    internal JsonObject WriteDocument(
        SchemaDescription root,
        IReadOnlyDictionary<string, SchemaDescription> definitions,
        JsonSchemaOptions options,
        bool inlineDefinitions,
        out Dictionary<string, JsonObject> writtenDefinitions)
    {
        var json = new JsonObject();

        if (options.IncludeDialect)
        {
            json["$schema"] = Dialect;
        }

        if (options.Id is not null)
        {
            json["$id"] = options.Id;
        }

        if (options.Title is not null)
        {
            json["title"] = options.Title;
        }

        WriteInto(json, root, string.Empty);

        writtenDefinitions = new Dictionary<string, JsonObject>(definitions.Count, StringComparer.Ordinal);

        foreach (var (name, description) in definitions)
        {
            writtenDefinitions[name] = Write(description, "$defs/" + name);
        }

        if (inlineDefinitions && writtenDefinitions.Count > 0)
        {
            var written = new JsonObject();
            foreach (var (name, node) in writtenDefinitions)
            {
                written[name] = node;
            }

            json["$defs"] = written;
        }

        return json;
    }

    private JsonObject Write(SchemaDescription description, string path)
    {
        var json = new JsonObject();
        WriteInto(json, description, path);
        return json;
    }

    private void WriteInto(JsonObject json, SchemaDescription description, string path)
    {
        // A reference stands alone among assertions. Anything written beside it in 2020-12 would be a
        // sibling constraint, which is legal and almost never what the author meant.
        if (description.Reference is not null)
        {
            json["$ref"] = description.Reference;

            // Annotations are not assertions, so they sit beside a reference without changing what it
            // means. 2020-12 allows siblings; earlier drafts did not, which is why this is worth a
            // sentence.
            WriteAnnotations(json, description);

            // A reference has no rules of its own, so it has nothing to lose and nothing to express.
            // Whatever it points at is written once, where it is defined, which is also what stops a
            // recursive schema from being written for ever.
            Offer(json, path, description, lost: null);
            return;
        }

        var lost = CollectLosses(description);

        WriteType(json, description);

        if (description.Format is not null)
        {
            json["format"] = description.Format;
        }

        if (description.Pattern is not null)
        {
            json["pattern"] = description.Pattern;
        }

        // 2020-12's own spelling, and the one OpenAPI 3.1 took for a binary part of a multipart
        // request. "format": "binary" is OpenAPI 3.0's, which this writer does not serve.
        if (description.ContentMediaType is not null)
        {
            json["contentMediaType"] = description.ContentMediaType;
        }

        WriteTextBounds(json, description);
        WriteNumericBounds(json, description);
        WriteArrayBounds(json, description, path);
        WriteComposition(json, description, path);
        WriteAnnotations(json, description);

        // Offered after its children, so an amendment on an object sees the fields it contains as they
        // will be read, including whatever an amendment on one of those fields did to them.
        Offer(json, path, description, lost);
    }

    // Hands the written node to the caller's amendment, then records whatever is still missing.
    //
    // The order is the answer to a design question with two plausible sides. The policy used to be
    // checked on the description, before anything was written, which meant an amendment that expressed
    // a rule itself still took the exception for not having expressed it. Reordering alone would not
    // have helped: the policy reads the description, and an amendment changes the document.
    //
    // So the amendment is given a way to say so. Expressing is per node rather than per rule, because
    // a caller who patched a node knows what they patched it for, and the writer cannot tell a pattern
    // that expresses a refinement from one that does not. Saying nothing still reports, which is the
    // conservative direction: a document believed complete that quietly is not is the failure this
    // policy exists to prevent.
    private void Offer(
        JsonObject json,
        string path,
        SchemaDescription description,
        List<string>? lost)
    {
        if (amend is not null)
        {
            var node = new JsonSchemaNode(
                path,
                json,
                description,
                (IReadOnlyList<string>?)lost ?? NothingLost);
            amend(node);

            if (node.Expressed)
            {
                return;
            }
        }

        if (lost is null)
        {
            return;
        }

        var where = path.Length == 0 ? "(root)" : path;

        foreach (var rule in lost)
        {
            _lost.Add(string.Create(CultureInfo.InvariantCulture, $"{where}: {rule}"));
        }
    }

    // What this node could not say: the rules that reported themselves unrepresentable, plus the
    // keywords a rule recorded in good faith and this writer has nowhere to put.
    //
    // The second kind is the quietest way to lose a constraint, because the rule believed it had been
    // recorded and the document does not have it. A temporal bound is the case that happens -- a date
    // is a string, and a range is numeric.
    private static List<string>? CollectLosses(SchemaDescription description)
    {
        List<string>? lost = null;

        if (description.Unrepresentable is { Count: > 0 } rules)
        {
            lost = [.. rules];
        }

        if (!Representable.Range(description.Kind))
        {
            if (description.Minimum is not null)
            {
                (lost ??= []).Add($"a minimum bound, which has no keyword for a {Name(description.Kind)}");
            }

            if (description.Maximum is not null)
            {
                (lost ??= []).Add($"a maximum bound, which has no keyword for a {Name(description.Kind)}");
            }

            if (description.MultipleOf is not null)
            {
                (lost ??= []).Add($"a divisor, which has no keyword for a {Name(description.Kind)}");
            }
        }

        if (!Representable.Length(description.Kind) &&
            (description.MinLength is not null || description.MaxLength is not null))
        {
            (lost ??= []).Add($"a length bound, which has no keyword for a {Name(description.Kind)}");
        }

        if (!Representable.Count(description.Kind) &&
            (description.MinItems is not null || description.MaxItems is not null))
        {
            (lost ??= []).Add($"a count bound, which has no keyword for a {Name(description.Kind)}");
        }

        return lost;
    }

    private static string Name(SchemaKind kind) => kind switch
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

    private static void WriteType(JsonObject json, SchemaDescription description)
    {
        // A schema of only null carries AllowsNull as well, because null is the one thing it accepts.
        // Writing both would produce ["null", "null"].
        if (description.Kind == SchemaKind.Null)
        {
            json["type"] = "null";
            return;
        }

        var name = description.Kind switch
        {
            SchemaKind.String => "string",
            SchemaKind.Integer => "integer",
            SchemaKind.Number => "number",
            SchemaKind.Boolean => "boolean",
            SchemaKind.Object => "object",
            SchemaKind.Array => "array",
            SchemaKind.Map => "object",
            _ => null,
        };

        if (name is null)
        {
            // A schema that did not describe itself constrains nothing. Saying so by omission is
            // honest; guessing a type would make the document wrong rather than incomplete.
            return;
        }

        // 2020-12 spells "or null" as a type union rather than a keyword of its own.
        json["type"] = description.AllowsNull
            ? new JsonArray(name, "null")
            : JsonValue.Create(name);
    }

    private static void WriteTextBounds(JsonObject json, SchemaDescription description)
    {
        if (!Representable.Length(description.Kind))
        {
            return;
        }

        if (description.MinLength is { } min)
        {
            json["minLength"] = min;
        }

        if (description.MaxLength is { } max)
        {
            json["maxLength"] = max;
        }
    }

    private static void WriteNumericBounds(JsonObject json, SchemaDescription description)
    {
        // A temporal schema describes its bounds to nobody: a date range has no keyword in this
        // dialect, and writing one that does not apply would make the document wrong rather than
        // merely incomplete. CollectLosses asks the same question of the same place, in the same walk,
        // so the two cannot drift apart.
        if (!Representable.Range(description.Kind))
        {
            return;
        }

        if (ToNode(description.Minimum) is { } minimum)
        {
            json[description.ExclusiveMinimum ? "exclusiveMinimum" : "minimum"] = minimum;
        }

        if (ToNode(description.Maximum) is { } maximum)
        {
            json[description.ExclusiveMaximum ? "exclusiveMaximum" : "maximum"] = maximum;
        }

        if (ToNode(description.MultipleOf) is { } divisor)
        {
            json["multipleOf"] = divisor;
        }
    }

    private void WriteArrayBounds(JsonObject json, SchemaDescription description, string path)
    {
        if (Representable.Count(description.Kind))
        {
            // A map and an array count the same thing under different keywords, so the count rules do
            // not need two implementations; the kind decides how the bound is spelled.
            var minimumKeyword = description.Kind == SchemaKind.Map ? "minProperties" : "minItems";
            var maximumKeyword = description.Kind == SchemaKind.Map ? "maxProperties" : "maxItems";

            if (description.MinItems is { } min)
            {
                json[minimumKeyword] = min;
            }

            if (description.MaxItems is { } max)
            {
                json[maximumKeyword] = max;
            }

            if (description.UniqueItems)
            {
                json["uniqueItems"] = true;
            }
        }

        // Outside the guard, because an element description belongs to whatever produced it and a node
        // that has one has to be walked whether or not this node's count keywords applied.
        if (description.Items is { } items)
        {
            json["items"] = Write(items, path + "[]");
        }
    }

    private void WriteComposition(JsonObject json, SchemaDescription description, string path)
    {
        if (description.Properties is { Count: > 0 } properties)
        {
            var written = new JsonObject();
            var required = new List<JsonNode?>();

            foreach (var property in properties)
            {
                written[property.Name] = Write(property.Schema, Join(path, property.Name));

                if (property.IsRequired)
                {
                    required.Add(JsonValue.Create(property.Name));
                }
            }

            json["properties"] = written;

            if (required.Count > 0)
            {
                json["required"] = new JsonArray([.. required]);
            }
        }

        if (description.AdditionalProperties is { } additional)
        {
            json["additionalProperties"] = Write(additional, Join(path, "*"));
        }

        if (description.AllowedValues is { Count: > 0 } allowed)
        {
            var values = new List<JsonNode?>(allowed.Count);
            foreach (var value in allowed)
            {
                values.Add(ToNode(value));
            }

            json["enum"] = new JsonArray([.. values]);
        }

        if (description.HasConstantValue)
        {
            json["const"] = ToNode(description.ConstantValue);
        }

        if (description.AnyOf is { Count: > 0 } anyOf)
        {
            json["anyOf"] = WriteAll(anyOf, path);
        }

        if (description.AllOf is { Count: > 0 } allOf)
        {
            json["allOf"] = WriteAll(allOf, path);
        }
    }

    // A branch of a union or an intersection sits at the same position as the value itself, so it adds
    // nothing to the path. Two branches therefore share one path, which is as precise as a path can be
    // about a value that has to satisfy one of several shapes; an amendment telling them apart does so
    // by looking at the node it was handed.
    private JsonArray WriteAll(IReadOnlyList<SchemaDescription> descriptions, string path)
    {
        var written = new List<JsonNode?>(descriptions.Count);
        foreach (var description in descriptions)
        {
            written.Add(Write(description, path));
        }

        return new JsonArray([.. written]);
    }

    private static void WriteAnnotations(JsonObject json, SchemaDescription description)
    {
        if (description.Title is not null)
        {
            json["title"] = description.Title;
        }

        if (description.Description is not null)
        {
            json["description"] = description.Description;
        }

        if (ToNode(description.Example) is { } example)
        {
            // "examples" and not "example": the singular is OpenAPI 3.0's spelling, and 2020-12 took
            // the plural.
            json["examples"] = new JsonArray(example);
        }

        if (description.IsDeprecated)
        {
            json["deprecated"] = true;
        }

        if (description.HasDefaultValue)
        {
            json["default"] = ToNode(description.DefaultValue);
        }
    }

    private static string Join(string path, string name) =>
        path.Length == 0 ? name : path + "." + name;

    // Values reach here boxed, because a bound preserves the caller's exact numeric type rather than
    // flattening everything to double. Each case is written out instead of handed to a serializer:
    // the serializer would need the type at run time, which is the reflection this library does not
    // do, and an enum member or a date has an obvious invariant spelling anyway.
    private static JsonValue? ToNode(object? value) => value switch
    {
        null => null,
        bool flag => JsonValue.Create(flag),
        string text => JsonValue.Create(text),

        // Round-trip formats, spelled out because the invariant culture's default for a DateTime is
        // "01/01/2026 00:00:00", which is not what any JSON document holds. TimeSpan uses "c", which
        // is what System.Text.Json itself writes.
        DateTime moment => JsonValue.Create(moment.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset moment => JsonValue.Create(moment.ToString("O", CultureInfo.InvariantCulture)),
        DateOnly date => JsonValue.Create(date.ToString("O", CultureInfo.InvariantCulture)),
        TimeOnly time => JsonValue.Create(time.ToString("O", CultureInfo.InvariantCulture)),
        TimeSpan duration => JsonValue.Create(duration.ToString("c", CultureInfo.InvariantCulture)),

        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        short number => JsonValue.Create(number),
        sbyte number => JsonValue.Create(number),
        byte number => JsonValue.Create(number),
        uint number => JsonValue.Create(number),
        ulong number => JsonValue.Create(number),
        ushort number => JsonValue.Create(number),
        decimal number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        float number => JsonValue.Create(number),
        IFormattable formattable => JsonValue.Create(
            formattable.ToString(null, CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(value.ToString()),
    };
}
