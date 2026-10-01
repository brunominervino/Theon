using System.Globalization;
using System.Text.Json.Nodes;
using Theon.Metadata;

namespace Theon.Json;

// Reads a JSON Schema document into the description model.
//
// The description model is the pivot in both directions. Going out, a schema produces one and a writer
// turns it into a document; coming in, a document becomes one and a validator runs it over a JsonNode.
// That is why this reads into SchemaDescription rather than inventing a representation of its own:
// there is already a model of "what an acceptable value looks like" that this repository owns, and a
// second one would be a second thing to keep in step.
//
// Everything here happens once, when the schema is built. A document that cannot be honoured is
// refused at that moment rather than at the first request, which is the same bargain every other
// construction-time check in this library makes.
internal static class JsonSchemaReader
{
    // Keywords that assert something about a value and that this reader does not implement. Refused
    // rather than ignored: ignoring an assertion makes the schema more permissive than the document,
    // which is the direction that lets a bad value through while the caller believes it was checked.
    private static readonly string[] Unsupported =
    [
        "oneOf",
        "not",
        "if",
        "then",
        "else",
        "patternProperties",
        "propertyNames",
        "prefixItems",
        "contains",
        "minContains",
        "maxContains",
        "dependentSchemas",
        "dependentRequired",
        "unevaluatedItems",
        "unevaluatedProperties",
        "contentSchema",
        "$dynamicRef",
        "$dynamicAnchor",
    ];

    internal static JsonSchemaDefinitions Read(JsonNode? document)
    {
        var pending = new Queue<string>();
        var definitions = new Dictionary<string, SchemaDescription>(StringComparer.Ordinal);

        var root = ReadNode(document, document, pending, definitions);

        // A reference is not followed where it is found: following it would never return on a schema
        // that contains itself. Instead the pointer is noted and read once, here, and every reference
        // to it resolves through the dictionary.
        while (pending.Count > 0)
        {
            var pointer = pending.Dequeue();

            if (definitions.ContainsKey(pointer))
            {
                continue;
            }

            // Claimed before reading, so that a schema referring to itself does not queue itself for
            // ever.
            definitions[pointer] = new SchemaDescription();
            definitions[pointer] = ReadNode(Resolve(document, pointer), document, pending, definitions);
        }

        return new JsonSchemaDefinitions(root, definitions);
    }

    private static SchemaDescription ReadNode(
        JsonNode? node,
        JsonNode? document,
        Queue<string> pending,
        Dictionary<string, SchemaDescription> definitions)
    {
        // 2020-12 lets a schema be a boolean. "true" accepts anything, which is an empty description;
        // "false" accepts nothing, which this model has no way to say.
        if (node is JsonValue flagValue && flagValue.TryGetValue<bool>(out var accepts))
        {
            return accepts
                ? new SchemaDescription()
                : throw new NotSupportedException(
                    "A schema of 'false' rejects every value, and there is no way to say that here. " +
                    "Say it with Refine on the result instead.");
        }

        if (node is not JsonObject json)
        {
            throw new NotSupportedException(
                $"A schema has to be an object or a boolean; this one is {Shape(node)}.");
        }

        foreach (var keyword in Unsupported)
        {
            if (json.ContainsKey(keyword))
            {
                throw new NotSupportedException(
                    $"'{keyword}' asserts something about a value that this reader cannot check, and " +
                    "leaving it out would make the schema accept values the document rejects. " +
                    "Supported: $ref, type, enum, const, minimum, maximum, exclusiveMinimum, " +
                    "exclusiveMaximum, multipleOf, minLength, maxLength, pattern, format, items, " +
                    "minItems, maxItems, uniqueItems, properties, required, additionalProperties, " +
                    "minProperties, maxProperties, anyOf and allOf.");
            }
        }

        if (json["$ref"] is { } reference)
        {
            var pointer = Text(reference, "$ref");

            if (!pointer.StartsWith('#'))
            {
                throw new NotSupportedException(
                    $"'{pointer}' points outside this document, and nothing here fetches one. " +
                    "Inline what it refers to, or read that document separately.");
            }

            pending.Enqueue(pointer);
            return new SchemaDescription { Reference = pointer };
        }

        var (kind, allowsNull) = ReadKind(json);

        // An object whose keys are not named and whose values are all governed by one schema is a map.
        // JSON has one kind for both, and the only thing that tells them apart is which keywords are
        // present -- which also decides whether a bound on how many there are is spelled minItems or
        // minProperties, so getting it wrong loses the bound on the way back out.
        if (kind == SchemaKind.Object &&
            json["properties"] is null &&
            json["additionalProperties"] is not null)
        {
            kind = SchemaKind.Map;
        }

        return new SchemaDescription
        {
            Kind = kind,
            AllowsNull = allowsNull,
            Format = json["format"]?.GetValue<string>(),
            Pattern = json["pattern"]?.GetValue<string>(),
            MinLength = Count(json, "minLength"),
            MaxLength = Count(json, "maxLength"),
            Minimum = OnlyOne(json, "minimum", "exclusiveMinimum", Bound),
            Maximum = OnlyOne(json, "maximum", "exclusiveMaximum", Bound),
            ExclusiveMinimum = json.ContainsKey("exclusiveMinimum"),
            ExclusiveMaximum = json.ContainsKey("exclusiveMaximum"),
            MultipleOf = Bound(json, "multipleOf"),
            MinItems = OnlyOne(json, "minItems", "minProperties", Count),
            MaxItems = OnlyOne(json, "maxItems", "maxProperties", Count),
            UniqueItems = json["uniqueItems"]?.GetValue<bool>() ?? false,
            Items = Child(json, "items", document, pending, definitions),
            AdditionalProperties = Child(json, "additionalProperties", document, pending, definitions),
            Properties = ReadProperties(json, document, pending, definitions),
            AnyOf = Branches(json, "anyOf", document, pending, definitions),
            AllOf = Branches(json, "allOf", document, pending, definitions),
            AllowedValues = ReadEnum(json),
            ConstantValue = json["const"] is { } constant ? Plain(constant) : null,
            HasConstantValue = json.ContainsKey("const"),
        };
    }

    // "type" is one name or a list of them. A list is how 2020-12 says "or null", which is the only
    // use of it this library makes and the only one it reads back: a list naming two real kinds would
    // be a union, and a description carries one kind.
    private static (SchemaKind Kind, bool AllowsNull) ReadKind(JsonObject json)
    {
        if (json["type"] is not { } type)
        {
            return (SchemaKind.Unknown, false);
        }

        if (type is JsonArray names)
        {
            var kind = SchemaKind.Unknown;
            var allowsNull = false;

            foreach (var name in names)
            {
                var text = Text(name, "type");

                if (text == "null")
                {
                    allowsNull = true;
                    continue;
                }


                if (kind != SchemaKind.Unknown)
                {
                    throw new NotSupportedException(
                        "A 'type' naming more than one kind of value, apart from null, is a union " +
                        "this reader cannot express. Write it as anyOf instead.");
                }

                kind = KindOf(text);
            }

            // A type set of nothing but null restricts the value to null, which is not the same as a
            // document that named no type at all. Reading it as the latter would accept everything.
            return kind == SchemaKind.Unknown && allowsNull
                ? (SchemaKind.Null, true)
                : (kind, allowsNull);
        }

        var single = Text(type, "type");
        return single == "null" ? (SchemaKind.Null, true) : (KindOf(single), false);
    }

    private static SchemaKind KindOf(string name) => name switch
    {
        "string" => SchemaKind.String,
        "integer" => SchemaKind.Integer,
        "number" => SchemaKind.Number,
        "boolean" => SchemaKind.Boolean,
        "object" => SchemaKind.Object,
        "array" => SchemaKind.Array,
        _ => throw new NotSupportedException($"'{name}' is not a JSON type."),
    };

    private static List<PropertyDescription>? ReadProperties(
        JsonObject json,
        JsonNode? document,
        Queue<string> pending,
        Dictionary<string, SchemaDescription> definitions)
    {
        var properties = json["properties"] as JsonObject;
        var required = new List<string>();

        if (json["required"] is JsonArray names)
        {
            foreach (var name in names)
            {
                required.Add(Text(name, "required"));
            }
        }

        if (properties is null && required.Count == 0)
        {
            return null;
        }

        var described = new List<PropertyDescription>((properties?.Count ?? 0) + required.Count);

        if (properties is not null)
        {
            foreach (var (name, schema) in properties)
            {
                described.Add(new PropertyDescription(
                    name,
                    ReadNode(schema, document, pending, definitions),
                    required.Contains(name)));
            }
        }

        // "required" is independent of "properties" in this dialect: a name may be demanded without
        // anything being said about what its value has to look like. Reading only the names that
        // "properties" also mentions would drop the demand, and a document saying
        // {"required": ["id"]} would accept {} -- which is the whole failure this reader refuses
        // elsewhere by throwing, and here it can simply be got right instead.
        foreach (var name in required)
        {
            if (properties is null || !properties.ContainsKey(name))
            {
                described.Add(new PropertyDescription(name, new SchemaDescription(), isRequired: true));
            }
        }

        return described;
    }

    private static SchemaDescription? Child(
        JsonObject json,
        string keyword,
        JsonNode? document,
        Queue<string> pending,
        Dictionary<string, SchemaDescription> definitions) =>
        json[keyword] is { } child ? ReadNode(child, document, pending, definitions) : null;

    private static List<SchemaDescription>? Branches(
        JsonObject json,
        string keyword,
        JsonNode? document,
        Queue<string> pending,
        Dictionary<string, SchemaDescription> definitions)
    {
        if (json[keyword] is not JsonArray array)
        {
            return null;
        }

        var branches = new List<SchemaDescription>(array.Count);

        foreach (var branch in array)
        {
            branches.Add(ReadNode(branch, document, pending, definitions));
        }

        return branches;
    }

    private static List<object?>? ReadEnum(JsonObject json)
    {
        if (json["enum"] is not JsonArray array)
        {
            return null;
        }

        var values = new List<object?>(array.Count);

        foreach (var value in array)
        {
            values.Add(Plain(value));
        }

        return values;
    }

    // Walks a JSON pointer from the document root. This is what makes both of the spellings in use
    // work -- "#/$defs/Comment" in a standalone document and "#/components/schemas/Comment" in an
    // OpenAPI description -- without either being a special case.
    private static JsonNode? Resolve(JsonNode? document, string pointer)
    {
        if (pointer is "#" or "#/")
        {
            return document;
        }

        var current = document;

        foreach (var raw in pointer[2..].Split('/'))
        {
            var token = raw.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            current = current switch
            {
                JsonObject json when json.TryGetPropertyValue(token, out var next) => next,
                JsonArray array when int.TryParse(token, CultureInfo.InvariantCulture, out var index)
                    && index >= 0 && index < array.Count => array[index],
                _ => throw new NotSupportedException(
                    $"'{pointer}' does not point at anything in this document."),
            };
        }

        return current;
    }

    // A description carries one bound where the dialect has two keywords for it, and the two are
    // independent assertions that may both appear. Taking one and dropping the other would make the
    // schema accept what the document rejects -- {"minimum":1,"exclusiveMinimum":5} would become
    // "greater than 1" and let 2 through -- so a document carrying both is refused rather than halved.
    //
    // Nothing this library writes carries both, which is why the round trip never meets it.
    private static T? OnlyOne<T>(
        JsonObject json,
        string keyword,
        string other,
        Func<JsonObject, string, T?> read)
    {
        if (json.ContainsKey(keyword) && json.ContainsKey(other))
        {
            throw new NotSupportedException(
                $"'{keyword}' and '{other}' are both present, and a description carries one bound " +
                "where the dialect has two keywords for it. Honouring one and dropping the other " +
                "would make this accept values the document rejects, so it is refused instead. " +
                "Write whichever of the two is the stricter and leave the other out.");
        }

        return read(json, keyword) ?? read(json, other);
    }

    private static int? Count(JsonObject json, string keyword) =>
        json[keyword] is { } value ? value.GetValue<int>() : null;

    // A bound keeps the shape the document wrote it in. A whole number stays whole, so a document
    // saying "maximum": 100 does not come back as 100.0 and read as a different document.
    private static object? Bound(JsonObject json, string keyword) =>
        json[keyword] is JsonValue value ? Number(value) : null;

    // A JsonValue holds whatever type it was made from, and only answers to that one. A node parsed
    // from text answers to anything numeric; one built by this library's own writer answers only to
    // the exact type the bound was declared with, which is the case that matters because reading our
    // own documents back is the test this whole direction is checked by.
    private static object Number(JsonValue value)
    {
        if (value.TryGetValue<int>(out var small))
        {
            return (long)small;
        }

        if (value.TryGetValue<long>(out var whole))
        {
            return whole;
        }

        if (value.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return decimal.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
    }

    private static object? Plain(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value => Number(value),
        _ => throw new NotSupportedException(
            "An allowed value has to be a string, a number, a boolean or null; an object or an " +
            "array cannot be compared here."),
    };

    private static string Text(JsonNode? node, string keyword) =>
        node?.GetValue<string>() ?? throw new NotSupportedException($"'{keyword}' has to be text.");

    private static string Shape(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "an array",
        JsonValue => "a plain value",
        _ => "something else",
    };
}

// What a document said, with whatever it referred to beside it.
internal sealed class JsonSchemaDefinitions(
    SchemaDescription root,
    IReadOnlyDictionary<string, SchemaDescription> definitions)
{
    internal SchemaDescription Root { get; } = root;

    internal IReadOnlyDictionary<string, SchemaDescription> Definitions { get; } = definitions;
}
