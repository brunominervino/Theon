namespace Theon.AspNetCore;

/// <summary>
/// Endpoint metadata naming the schema that guards an endpoint, so that a document generator can find
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <c>Validate</c> attaches one of these. It exists so that whatever produces the OpenAPI description
/// for an application can ask each endpoint what it actually requires, rather than inferring a request
/// body from the handler parameter type and stopping there. A generator reads it through
/// <c>endpoint.Metadata.GetMetadata&lt;TheonSchemaMetadata&gt;()</c>.
/// </para>
/// <para>
/// Deliberately not a document transformer of its own. The built-in OpenAPI pipeline arrived in
/// .NET 9, and this package also targets net8.0, where the same job belongs to Swashbuckle or NSwag.
/// A transformer here would either be conditional on the target framework or would drop net8.0, and
/// both are decisions about the package rather than about this type. Metadata serves every generator
/// and commits to none.
/// </para>
/// <para>
/// Nothing is generated until it is asked for. An application that never produces a description pays
/// nothing beyond one small object per endpoint.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a document transformer, for the endpoints that carry one:
/// var described = endpoint.Metadata
///     .GetMetadata&lt;TheonSchemaMetadata&gt;()?
///     .Describe(new JsonSchemaOptions
///     {
///         IncludeDialect = false,
///         ReferencePrefix = "#/components/schemas/",
///     });
/// </code>
/// </example>
public sealed class TheonSchemaMetadata
{
    private readonly Func<JsonSchemaOptions?, JsonSchemaDocument> _describe;

    internal TheonSchemaMetadata(
        Type validatedType,
        Func<JsonSchemaOptions?, JsonSchemaDocument> describe)
    {
        ValidatedType = validatedType;
        _describe = describe;
    }

    /// <summary>Gets the type the endpoint validates.</summary>
    public Type ValidatedType { get; }

    /// <summary>Describes the schema as JSON Schema.</summary>
    /// <param name="options">
    /// Options for the document, or <see langword="null"/> for the defaults. Set
    /// <see cref="JsonSchemaOptions.ReferencePrefix"/> to point at wherever the surrounding document
    /// keeps its shared schemas.
    /// </param>
    /// <remarks>
    /// Generated on each call rather than cached, because a caller may want it written differently for
    /// two documents and because this runs while a description is being produced rather than while a
    /// request is being served.
    /// </remarks>
    public JsonSchemaDocument Describe(JsonSchemaOptions? options = null) => _describe(options);
}
