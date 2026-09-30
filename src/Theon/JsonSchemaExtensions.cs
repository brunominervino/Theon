using System.Text.Json.Nodes;
using Theon.Metadata;

namespace Theon;

/// <summary>What to do about a rule a generated document cannot express.</summary>
public enum UnrepresentablePolicy
{
    /// <summary>Leave the rule out of the document, silently.</summary>
    /// <remarks>
    /// The default, and the right one for a document meant to be read: an incomplete document is still
    /// useful, and every schema of any size has at least one refinement in it.
    /// </remarks>
    Omit = 0,

    /// <summary>Throw, naming every rule that could not be expressed and where it was.</summary>
    /// <remarks>
    /// For a document that is the contract rather than the documentation. Where a client generated from
    /// it will be the only thing checking a value before it arrives, a rule missing from the document is
    /// a rule nothing enforces, and finding that out at start-up beats finding it out in production.
    /// </remarks>
    Throw,
}

/// <summary>Options for generating a JSON Schema document.</summary>
public sealed class JsonSchemaOptions
{
    /// <summary>Gets the options used when none are supplied.</summary>
    public static JsonSchemaOptions Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the document declares its dialect with <c>$schema</c>.
    /// </summary>
    /// <remarks>
    /// On by default, which is right for a document that stands on its own. Turn it off when the
    /// result is going to be embedded in a larger document that declares the dialect itself, such as
    /// an OpenAPI description.
    /// </remarks>
    public bool IncludeDialect { get; init; } = true;

    /// <summary>Gets a title for the document.</summary>
    /// <remarks>
    /// The title of the document as a whole. A title for one schema within it belongs on that schema,
    /// through <c>Annotate</c>.
    /// </remarks>
    public string? Title { get; init; }

    /// <summary>Gets an identifier for the document, written as <c>$id</c>.</summary>
    /// <remarks>
    /// The base that references elsewhere resolve against, so a schema can be pointed at from another
    /// document rather than copied into it.
    /// </remarks>
    public string? Id { get; init; }

    /// <summary>Gets the prefix every reference to a repeated schema is written with.</summary>
    /// <remarks>
    /// <c>#/$defs/</c> by default, which is what a standalone document uses. Set it to
    /// <c>#/components/schemas/</c> for an OpenAPI description, and take the definitions from
    /// <see cref="JsonSchemaDocument.Definitions"/> rather than from the document.
    /// </remarks>
    public string ReferencePrefix { get; init; } = "#/$defs/";

    /// <summary>Gets what to do about a rule the document cannot express.</summary>
    public UnrepresentablePolicy OnUnrepresentable { get; init; }
}

/// <summary>
/// A generated document, with the schemas it refers to kept separate from it.
/// </summary>
/// <remarks>
/// The two are apart because the two places a document goes want them apart. A standalone file wants
/// the definitions inside it under <c>$defs</c>, which is what <c>ToJsonSchema</c> produces. An OpenAPI
/// description wants them in <c>components/schemas</c>, shared with every other operation, and for that
/// the caller needs them in hand.
/// </remarks>
public sealed class JsonSchemaDocument
{
    internal JsonSchemaDocument(JsonObject root, IReadOnlyDictionary<string, JsonObject> definitions)
    {
        Root = root;
        Definitions = definitions;
    }

    /// <summary>Gets the schema itself, which may be a reference into <see cref="Definitions"/>.</summary>
    public JsonObject Root { get; }

    /// <summary>Gets the schemas the document refers to, by the name each reference uses.</summary>
    /// <remarks>
    /// Empty unless something repeated, which in practice means unless a schema referred to itself.
    /// </remarks>
    public IReadOnlyDictionary<string, JsonObject> Definitions { get; }
}

/// <summary>Generates a JSON Schema document from a schema.</summary>
public static class JsonSchemaExtensions
{
    /// <summary>
    /// Describes this schema as a self-contained JSON Schema document.
    /// </summary>
    /// <typeparam name="TInput">The type the schema accepts.</typeparam>
    /// <typeparam name="TOutput">The type the schema produces.</typeparam>
    /// <param name="schema">The schema to describe.</param>
    /// <param name="options">Options for the document, or <see langword="null"/> for the defaults.</param>
    /// <returns>The document, as a mutable <see cref="JsonObject"/>.</returns>
    /// <remarks>
    /// <para>
    /// The dialect is JSON Schema 2020-12, which is also the one OpenAPI 3.1 uses, so the result drops
    /// straight into an OpenAPI description. OpenAPI 3.0 is not supported: it predates that alignment
    /// and spells nullability its own way, so serving it would mean a second generator to keep in
    /// step with this one.
    /// </para>
    /// <para>
    /// The result is a <see cref="JsonObject"/> rather than a string so that a caller can add to it —
    /// a vendor extension, whatever the surrounding document needs — without parsing text back. Call
    /// <c>ToJsonString</c> on it for the text.
    /// </para>
    /// <para>
    /// A schema that repeats within the document, which is what a recursive schema does, is written
    /// once under <c>$defs</c> and referred to by <c>$ref</c> everywhere else. This relies on the
    /// repetition being the same instance, so a <see cref="Theo.Lazy"/> factory that builds a fresh
    /// schema on every call has no repetition to recognise; that case throws rather than running until
    /// the stack ends, and the message says what to change.
    /// </para>
    /// <para>
    /// What the document cannot say is what a <c>Refine</c> checks, what a normalization rewrites, or
    /// where a date range falls — none of those have a keyword that applies. They are left out rather
    /// than guessed at, because a document that omits a rule is incomplete and one that invents a rule
    /// is wrong. Set <see cref="JsonSchemaOptions.OnUnrepresentable"/> to be told about them instead,
    /// and use <c>Annotate</c> to describe such a rule in prose.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var document = Theo.Object&lt;CreateUser&gt;()
    ///     .Field(x =&gt; x.Email, Theo.String().Email().Annotate(description: "Where we write to you."))
    ///     .Field(x =&gt; x.Age, Theo.Int().Min(18).Max(120))
    ///     .ToJsonSchema(new JsonSchemaOptions { Title = "CreateUser" });
    ///
    /// Console.WriteLine(document.ToJsonString());
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// The schema nested deeper than the generator will follow, which a recursive schema built from a
    /// factory that returns a new instance each time will do; or a rule could not be expressed and
    /// <see cref="JsonSchemaOptions.OnUnrepresentable"/> asked to be told.
    /// </exception>
    public static JsonObject ToJsonSchema<TInput, TOutput>(
        this Schema<TInput, TOutput> schema,
        JsonSchemaOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        options ??= JsonSchemaOptions.Default;

        var described = Describe(schema, options);

        return JsonSchemaWriter.WriteDocument(
            described.Root,
            described.Definitions,
            options.IncludeDialect,
            options.Title,
            options.Id,
            inlineDefinitions: true);
    }

    /// <summary>
    /// Describes this schema, keeping the schemas it refers to separate from the document.
    /// </summary>
    /// <typeparam name="TInput">The type the schema accepts.</typeparam>
    /// <typeparam name="TOutput">The type the schema produces.</typeparam>
    /// <param name="schema">The schema to describe.</param>
    /// <param name="options">Options for the document, or <see langword="null"/> for the defaults.</param>
    /// <remarks>
    /// For putting a schema into a document that already has somewhere to keep shared definitions,
    /// which an OpenAPI description does. Set <see cref="JsonSchemaOptions.ReferencePrefix"/> to
    /// <c>#/components/schemas/</c> and copy <see cref="JsonSchemaDocument.Definitions"/> into that
    /// section.
    /// </remarks>
    /// <example>
    /// <code>
    /// var described = requestSchema.ToJsonSchemaDocument(new JsonSchemaOptions
    /// {
    ///     IncludeDialect = false,
    ///     ReferencePrefix = "#/components/schemas/",
    /// });
    ///
    /// foreach (var (name, json) in described.Definitions)
    /// {
    ///     components[name] = json;
    /// }
    /// </code>
    /// </example>
    /// <inheritdoc cref="ToJsonSchema" path="/exception"/>
    public static JsonSchemaDocument ToJsonSchemaDocument<TInput, TOutput>(
        this Schema<TInput, TOutput> schema,
        JsonSchemaOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        options ??= JsonSchemaOptions.Default;

        var described = Describe(schema, options);

        var root = JsonSchemaWriter.WriteDocument(
            described.Root,
            described.Definitions,
            options.IncludeDialect,
            options.Title,
            options.Id,
            inlineDefinitions: false);

        var separate = new Dictionary<string, JsonObject>(
            described.Definitions.Count,
            StringComparer.Ordinal);

        foreach (var (name, description) in described.Definitions)
        {
            separate[name] = JsonSchemaWriter.Write(description);
        }

        return new JsonSchemaDocument(root, separate);
    }

    private static (SchemaDescription Root, IReadOnlyDictionary<string, SchemaDescription> Definitions)
        Describe<TInput, TOutput>(Schema<TInput, TOutput> schema, JsonSchemaOptions options)
    {
        var context = new DescriptionContext(options.ReferencePrefix);
        var root = context.Describe(schema);

        if (options.OnUnrepresentable == UnrepresentablePolicy.Throw)
        {
            var missing = UnrepresentableWalk.Collect(root, context.Definitions);

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "These rules have no keyword in JSON Schema 2020-12 and were left out of the " +
                    "document, so nothing reading it will enforce them:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, missing.Select(static rule => "  " + rule)) +
                    Environment.NewLine +
                    "Describe them with Annotate, or leave OnUnrepresentable at Omit to accept an " +
                    "incomplete document.");
            }
        }

        return (root, context.Definitions);
    }
}
