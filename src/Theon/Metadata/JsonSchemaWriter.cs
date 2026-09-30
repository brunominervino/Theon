using System.Globalization;
using System.Text.Json.Nodes;

namespace Theon.Metadata;

// Turns a description into a JSON Schema object.
//
// This is the one place that switches on what kind of thing it is looking at, and it switches on the
// description model rather than on schema types. That is the whole point of having a description
// model: the rule against branching on schema identity protects the parse path, and a document
// generator is exactly where that rule would otherwise be broken first.
//
// The dialect is JSON Schema 2020-12, which is also what OpenAPI 3.1 uses, so one writer serves
// both. OpenAPI 3.0 is deliberately not supported: it predates that alignment, spells nullability
// with a keyword of its own and has no $defs, so serving it would mean a second writer to keep in
// step with this one.
internal static class JsonSchemaWriter
{
    internal const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    // Writes the whole document: the dialect, the root schema, and the definitions the root and its
    // children referred to. The keys go in this order because a reader opens the document at the top,
    // and a node cannot be moved between parents afterwards to reorder them.
    internal static JsonObject WriteDocument(
        SchemaDescription root,
        IReadOnlyDictionary<string, SchemaDescription> definitions,
        bool includeDialect,
        string? title,
        string? id,
        bool inlineDefinitions)
    {
        var json = new JsonObject();

        if (includeDialect)
        {
            json["$schema"] = Dialect;
        }

        if (id is not null)
        {
            json["$id"] = id;
        }

        if (title is not null)
        {
            json["title"] = title;
        }

        WriteInto(json, root);

        if (inlineDefinitions && definitions.Count > 0)
        {
            var written = new JsonObject();
            foreach (var (name, description) in definitions)
            {
                written[name] = Write(description);
            }

            json["$defs"] = written;
        }

        return json;
    }

    internal static JsonObject Write(SchemaDescription description)
    {
        var json = new JsonObject();
        WriteInto(json, description);
        return json;
    }

    private static void WriteInto(JsonObject json, SchemaDescription description)
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
            return;
        }

        WriteType(json, description);

        if (description.Format is not null)
        {
            json["format"] = description.Format;
        }

        if (description.Pattern is not null)
        {
            json["pattern"] = description.Pattern;
        }

        WriteTextBounds(json, description);
        WriteNumericBounds(json, description);
        WriteArrayBounds(json, description);
        WriteComposition(json, description);
        WriteAnnotations(json, description);
    }

    private static void WriteType(JsonObject json, SchemaDescription description)
    {
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
        // merely incomplete. The walk that reports what was left out asks the same question of the
        // same place, so the two cannot drift apart.
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

    private static void WriteArrayBounds(JsonObject json, SchemaDescription description)
    {
        if (!Representable.Count(description.Kind))
        {
            return;
        }

        // A map and an array count the same thing under different keywords, so the count rules do not
        // need two implementations; the kind decides how the bound is spelled.
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

        if (description.Items is { } items)
        {
            json["items"] = Write(items);
        }
    }

    private static void WriteComposition(JsonObject json, SchemaDescription description)
    {
        if (description.Properties is { Count: > 0 } properties)
        {
            var written = new JsonObject();
            var required = new List<JsonNode?>();

            foreach (var property in properties)
            {
                written[property.Name] = Write(property.Schema);

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
            json["additionalProperties"] = Write(additional);
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
            json["anyOf"] = WriteAll(anyOf);
        }

        if (description.AllOf is { Count: > 0 } allOf)
        {
            json["allOf"] = WriteAll(allOf);
        }
    }

    private static JsonArray WriteAll(List<SchemaDescription> descriptions)
    {
        var written = new List<JsonNode?>(descriptions.Count);
        foreach (var description in descriptions)
        {
            written.Add(Write(description));
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
