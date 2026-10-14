using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Theon.AspNetCore;

/// <summary>What a validation failure answers with.</summary>
/// <remarks>
/// <para>
/// Set once for an application with <c>AddTheon</c>, or for one endpoint by passing a configuration
/// to <c>Validate</c>. Every default is what this package answered before these options existed, so
/// an application that configures nothing sees no change.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddTheon(options =&gt;
/// {
///     options.Status = StatusCodes.Status422UnprocessableEntity;
///     options.Title = "Falha de validacao";
///     options.Code = "validacao";
///     options.IncludeErrorCodes = true;
///     options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
/// });
/// </code>
/// </example>
public sealed class TheonValidationOptions
{
    /// <summary>Gets or sets the status code a failure answers with.</summary>
    /// <remarks>
    /// 400 by default, which is what this package always answered. 422 is the other common choice:
    /// the body parsed, so the request was understood, and it is its contents that were refused.
    /// </remarks>
    public int Status { get; set; } = StatusCodes.Status400BadRequest;

    /// <summary>Gets or sets the <c>type</c> of the problem document.</summary>
    /// <remarks>
    /// Left alone by default, which lets ASP.NET Core fill in the one that matches the status.
    /// </remarks>
    public string? Type { get; set; }

    /// <summary>Gets or sets the <c>title</c> of the problem document.</summary>
    /// <inheritdoc cref="Type" path="/remarks"/>
    public string? Title { get; set; }

    /// <summary>Gets or sets a value written as a <c>code</c> member of the problem document.</summary>
    /// <remarks>
    /// For an application whose clients branch on one identifier per kind of failure —
    /// <c>"validacao"</c> beside whatever it answers for an authorization failure. Null writes no
    /// such member.
    /// </remarks>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each error carries its code beside its message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off by default, and turning it on changes the shape of <c>errors</c>: each entry becomes an
    /// object with <c>message</c> and <c>code</c> rather than a bare string. That is a break for
    /// anything reading the response, which is why it is opt-in rather than the default.
    /// </para>
    /// <para>
    /// The code is the one the rule carried — <see cref="Errors.ValidationError.CustomCode"/> — and
    /// the name of <see cref="Errors.ValidationError.Code"/> where the rule named nothing.
    /// </para>
    /// </remarks>
    public bool IncludeErrorCodes { get; set; }

    /// <summary>Gets or sets the policy the paths keying <c>errors</c> are named with.</summary>
    /// <remarks>
    /// Null by default, which reports the C# member name. Set it to whatever the application
    /// serializes with and the key of an error matches the field the client sent.
    /// </remarks>
    public JsonNamingPolicy? PropertyNamingPolicy { get; set; }

    internal static TheonValidationOptions Default { get; } = new();
}
