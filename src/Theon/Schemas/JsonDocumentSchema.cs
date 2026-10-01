using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Theon.Checks;
using Theon.Errors;
using Theon.Json;
using Theon.Metadata;

namespace Theon.Schemas;

// Validates an already-parsed JSON document against a description read from a JSON Schema.
//
// The first schema in this library whose input is not of a known shape, and it is still within
// decision 1: a JsonNode has been through System.Text.Json already, so this validates a materialized
// value like everything else. What it does not do is produce a typed object -- that would need
// reflection over property names, which is the thing decision 1 exists to avoid.
//
// It walks a SchemaDescription rather than a representation of its own. That model already says what
// an acceptable value looks like, and a second one would be a second thing to keep in step with the
// writer.
internal sealed class JsonDocumentSchema : Schema<JsonNode?>
{
    private readonly SchemaDescription _root;
    private readonly IReadOnlyDictionary<string, JsonDocumentSchema> _referenced;
    private readonly Regex? _pattern;
    private readonly Schema<string>? _format;

    internal JsonDocumentSchema(
        SchemaDescription root,
        IReadOnlyDictionary<string, JsonDocumentSchema> referenced)
    {
        _root = root;
        _referenced = referenced;
        _pattern = Compile(root.Pattern);
        _format = root.Format is null ? null : JsonFormats.For(root.Format);
    }

    // Built once, when the schema is. A pattern that came out of a document is text from a stranger,
    // so it is non-backtracking by construction rather than guarded by a timeout -- and the engine
    // refuses outright to build a pattern whose automaton is too large, which is the one case worth a
    // sentence of its own because the message it throws does not explain itself.
    private static Regex? Compile(string? pattern)
    {
        if (pattern is null)
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new NotSupportedException(
                $"The pattern '{pattern}' cannot be matched in linear time, so this will not use it: " +
                "a pattern out of somebody else's document is text from a stranger, and a backtracking " +
                "match over it is a way to make this process stop responding. Lookaround and " +
                "backreferences are the usual cause, and a very large pattern is the other. Express it " +
                "with Refine if you are sure.",
                exception);
        }
    }

    public override bool TryParse(ref ParseContext context, JsonNode? input, out JsonNode? output)
    {
        output = input;

        var errorsBefore = context.ErrorCount;
        Check(ref context, _root, input, this, followed: 0);

        return context.ErrorCount == errorsBefore;
    }

    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Substitute(_root, context);
    }

    // A reference in the description points at a definition by JSON pointer. A document generated from
    // here points at it by schema instance instead, which is what the generator's own cycle-breaking
    // recognises -- so a recursive document read in comes back out as a recursive document, named after
    // the CLR type rather than after whatever the original called it.
    private SchemaDescription Substitute(SchemaDescription node, DescriptionContext context)
    {
        if (node.Reference is { } pointer)
        {
            return context.Describe(_referenced[pointer]);
        }

        return new SchemaDescription(node)
        {
            Items = node.Items is null ? null : Substitute(node.Items, context),
            AdditionalProperties = node.AdditionalProperties is null
                ? null
                : Substitute(node.AdditionalProperties, context),
            Properties = node.Properties?
                .Select(property => new PropertyDescription(
                    property.Name,
                    Substitute(property.Schema, context),
                    property.IsRequired))
                .ToList(),
            AnyOf = node.AnyOf?.Select(branch => Substitute(branch, context)).ToList(),
            AllOf = node.AllOf?.Select(branch => Substitute(branch, context)).ToList(),
        };
    }

    // The one walk. It switches on the description, which is a model this repository owns, and never on
    // the identity of a schema -- the same reason the writer is allowed to switch.
    //
    // "followed" counts the references taken to reach this node, and it is not the same thing as the
    // depth of the value. The value's depth only advances when a property or an index is pushed, so a
    // schema whose reference comes back to itself through anyOf or allOf -- which is legal, and which
    // nothing here writes -- would recurse with the value standing still and never reach the limit.
    // That is a stack overflow, which cannot be caught, from a document somebody else published.
    // Counting the references bounds the schema as well as the value.
    private static void Check(
        ref ParseContext context,
        SchemaDescription node,
        JsonNode? value,
        JsonDocumentSchema owner,
        int followed)
    {
        if (node.Reference is { } pointer)
        {
            if (context.IsAtMaxDepth || followed >= context.Options.MaxDepth)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.Custom,
                    Expected = "a value nested less deeply",
                });

                return;
            }

            var referenced = owner._referenced[pointer];
            Check(ref context, referenced._root, value, referenced, followed + 1);
            return;
        }

        var actual = KindOf(value);

        if (actual is null)
        {
            // JSON null. Refused only where the document named a type and did not include null; a
            // document that named no type constrains nothing about what kind of value this is.
            if (node.Kind != SchemaKind.Unknown && !node.AllowsNull)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.InvalidType,
                    Expected = Name(node.Kind),
                    Received = "null",
                });

                return;
            }
        }
        else if (!Matches(node.Kind, actual.Value))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = Name(node.Kind),
                Received = Name(actual.Value),
            });

            return;
        }

        CheckAllowedValues(ref context, node, value);

        switch (actual)
        {
            case SchemaKind.String:
                CheckString(ref context, node, value!.GetValue<string>(), owner);
                break;
            case SchemaKind.Integer or SchemaKind.Number:
                CheckNumber(ref context, node, value!.GetValue<decimal>());
                break;
            case SchemaKind.Array:
                CheckArray(ref context, node, value!.AsArray(), owner, followed);
                break;
            case SchemaKind.Object:
                CheckObject(ref context, node, value!.AsObject(), owner, followed);
                break;
            default:
                break;
        }

        CheckComposition(ref context, node, value, owner, followed);
    }

    private static void CheckAllowedValues(
        ref ParseContext context,
        SchemaDescription node,
        JsonNode? value)
    {
        if (node.AllowedValues is { Count: > 0 } allowed)
        {
            var found = false;

            foreach (var candidate in allowed)
            {
                if (Equal(candidate, value))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidValue });
            }
        }

        if (node.HasConstantValue && !Equal(node.ConstantValue, value))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.NotEqual,
                Expected = node.ConstantValue?.ToString() ?? "null",
            });
        }
    }

    private static void CheckString(
        ref ParseContext context,
        SchemaDescription node,
        string value,
        JsonDocumentSchema owner)
    {
        if (node.MinLength is { } minimum && StringMeasure.IsShorterThan(value, minimum))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Text,
                Minimum = minimum,
                Inclusive = true,
            });
        }

        if (node.MaxLength is { } maximum && StringMeasure.IsLongerThan(value, maximum))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Text,
                Maximum = maximum,
                Inclusive = true,
            });
        }

        if (owner._pattern is { } pattern && !pattern.IsMatch(value))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Format = "regex",
            });
        }

        owner._format?.TryParse(ref context, value, out _);
    }

    private static void CheckNumber(ref ParseContext context, SchemaDescription node, decimal value)
    {
        if (node.Minimum is { } minimum)
        {
            var bound = Convert.ToDecimal(minimum, CultureInfo.InvariantCulture);

            if (node.ExclusiveMinimum ? value <= bound : value < bound)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Number,
                    Minimum = minimum,
                    Inclusive = !node.ExclusiveMinimum,
                });
            }
        }

        if (node.Maximum is { } maximum)
        {
            var bound = Convert.ToDecimal(maximum, CultureInfo.InvariantCulture);

            if (node.ExclusiveMaximum ? value >= bound : value > bound)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Number,
                    Maximum = maximum,
                    Inclusive = !node.ExclusiveMaximum,
                });
            }
        }

        if (node.MultipleOf is { } divisor)
        {
            var step = Convert.ToDecimal(divisor, CultureInfo.InvariantCulture);

            if (step != 0 && value % step != 0)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.NotMultipleOf,
                    Divisor = divisor,
                });
            }
        }
    }

    private static void CheckArray(
        ref ParseContext context,
        SchemaDescription node,
        JsonArray value,
        JsonDocumentSchema owner,
        int followed)
    {
        CheckCount(ref context, node, value.Count);

        if (node.UniqueItems)
        {
            for (var i = 1; i < value.Count; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (JsonNode.DeepEquals(value[i], value[j]))
                    {
                        context.PushIndex(i);
                        context.AddError(new ValidationErrorInfo
                        {
                            Code = ValidationErrorCode.Duplicate,
                        });
                        context.Pop();
                        break;
                    }
                }
            }
        }

        if (node.Items is not { } items)
        {
            return;
        }

        for (var i = 0; i < value.Count; i++)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.PushIndex(i);
            Check(ref context, items, value[i], owner, followed);
            context.Pop();
        }
    }

    private static void CheckObject(
        ref ParseContext context,
        SchemaDescription node,
        JsonObject value,
        JsonDocumentSchema owner,
        int followed)
    {
        CheckCount(ref context, node, value.Count);

        if (node.Properties is { } properties)
        {
            foreach (var property in properties)
            {
                if (context.ShouldStop)
                {
                    break;
                }

                if (!value.TryGetPropertyValue(property.Name, out var held))
                {
                    // A missing key is a real thing here, unlike everywhere else in this library,
                    // because this is the one schema whose input has not been through a type yet.
                    if (property.IsRequired)
                    {
                        context.PushProperty(property.Name);
                        context.AddError(new ValidationErrorInfo
                        {
                            Code = ValidationErrorCode.InvalidType,
                            Expected = Name(property.Schema.Kind),
                            Received = "null",
                        });
                        context.Pop();
                    }

                    continue;
                }

                context.PushProperty(property.Name);
                Check(ref context, property.Schema, held, owner, followed);
                context.Pop();
            }
        }

        if (node.AdditionalProperties is not { } additional)
        {
            return;
        }

        foreach (var (name, held) in value)
        {
            if (context.ShouldStop)
            {
                break;
            }

            // additionalProperties governs the keys "properties" did not name.
            if (Names(node, name))
            {
                continue;
            }

            context.PushProperty(name);
            Check(ref context, additional, held, owner, followed);
            context.Pop();
        }
    }

    private static bool Names(SchemaDescription node, string name)
    {
        if (node.Properties is not { } properties)
        {
            return false;
        }

        foreach (var property in properties)
        {
            if (property.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckCount(ref ParseContext context, SchemaDescription node, int count)
    {
        if (node.MinItems is { } minimum && count < minimum)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Collection,
                Minimum = minimum,
                Inclusive = true,
            });
        }

        if (node.MaxItems is { } maximum && count > maximum)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Collection,
                Maximum = maximum,
                Inclusive = true,
            });
        }
    }

    private static void CheckComposition(
        ref ParseContext context,
        SchemaDescription node,
        JsonNode? value,
        JsonDocumentSchema owner,
        int followed)
    {
        if (node.AnyOf is { Count: > 0 } alternatives)
        {
            var satisfied = false;

            foreach (var alternative in alternatives)
            {
                var attempt = context.Fork();
                Check(ref attempt, alternative, value, owner, followed);

                if (!attempt.HasErrors)
                {
                    satisfied = true;
                    break;
                }
            }

            if (!satisfied)
            {
                context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.InvalidValue });
            }
        }

        if (node.AllOf is { Count: > 0 } all)
        {
            foreach (var one in all)
            {
                Check(ref context, one, value, owner, followed);
            }
        }
    }

    private static bool Matches(SchemaKind declared, SchemaKind actual) => declared switch
    {
        SchemaKind.Unknown => true,

        // A map and an object are the same JSON value; the distinction is whether the keys were named,
        // which is about the schema and not about what arrived.
        SchemaKind.Object or SchemaKind.Map => actual == SchemaKind.Object,

        // Every integer is a number. The reverse is not true, which is what "integer" exists to say.
        SchemaKind.Number => actual is SchemaKind.Number or SchemaKind.Integer,
        _ => declared == actual,
    };

    private static SchemaKind? KindOf(JsonNode? value) => value switch
    {
        null => null,
        JsonObject => SchemaKind.Object,
        JsonArray => SchemaKind.Array,
        JsonValue plain when plain.TryGetValue<bool>(out _) => SchemaKind.Boolean,
        JsonValue plain when plain.TryGetValue<string>(out _) => SchemaKind.String,

        // "integer" in this dialect means a number with nothing after the point, not a particular
        // encoding, so 1.0 is an integer and 1.5 is not.
        JsonValue plain when plain.TryGetValue<decimal>(out var number) =>
            decimal.Truncate(number) == number ? SchemaKind.Integer : SchemaKind.Number,
        _ => SchemaKind.Unknown,
    };

    private static bool Equal(object? expected, JsonNode? actual) => (expected, actual) switch
    {
        (null, null) => true,
        (_, null) => false,
        (string text, JsonValue plain) => plain.TryGetValue<string>(out var held) && held == text,
        (bool flag, JsonValue plain) => plain.TryGetValue<bool>(out var held) && held == flag,
        (long whole, JsonValue plain) => plain.TryGetValue<decimal>(out var held) && held == whole,
        (decimal number, JsonValue plain) => plain.TryGetValue<decimal>(out var held) && held == number,
        _ => false,
    };

    private static string Name(SchemaKind kind) => kind switch
    {
        SchemaKind.String => "string",
        SchemaKind.Integer => "integer",
        SchemaKind.Number => "number",
        SchemaKind.Boolean => "boolean",
        SchemaKind.Object or SchemaKind.Map => "object",
        SchemaKind.Array => "array",
        _ => "value",
    };
}
