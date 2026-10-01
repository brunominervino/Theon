namespace Theon;

/// <summary>Which side of a schema a description describes.</summary>
/// <remarks>
/// <para>
/// Only a schema that transforms has two sides to choose between, and those are exactly the schemas
/// worth describing carefully: a request body is what the caller sends, and a response body is what
/// the program produced, and the same schema says both.
/// </para>
/// <para>
/// A normalization is not a transformation and is not modelled here. <c>Trim</c> narrows what comes
/// out without changing its type, and the dialect has no way to say "this has no leading space", so
/// both sides of a normalized value are described identically.
/// </para>
/// </remarks>
public enum DescriptionDirection
{
    /// <summary>Describe what the schema accepts.</summary>
    /// <remarks>
    /// The default, and the right side for a request body, a query string or a configuration file —
    /// anywhere the document tells somebody else what to send.
    /// </remarks>
    Input = 0,

    /// <summary>Describe what the schema produces.</summary>
    /// <remarks>
    /// The right side for a response body, where the document tells a reader what they will receive.
    /// </remarks>
    Output,
}
