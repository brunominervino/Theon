using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Theon.AspNetCore;

/// <summary>
/// Declares which schema guards an endpoint.
/// </summary>
public static class ValidationEndpointExtensions
{
    /// <summary>
    /// Validates the endpoint's <typeparamref name="T"/> argument before the handler runs, and
    /// answers with a validation problem response if it does not satisfy <paramref name="schema"/>.
    /// </summary>
    /// <typeparam name="T">The type to validate, which the handler must accept as a parameter.</typeparam>
    /// <param name="builder">The endpoint being built.</param>
    /// <param name="schema">The schema the argument must satisfy.</param>
    /// <remarks>
    /// <para>
    /// The schema is named here rather than discovered from the service container. Both work, and
    /// this one puts the rule where the endpoint is declared: a reader sees what the endpoint
    /// accepts and what it requires of it in the same three lines, without going to find a
    /// registration somewhere else. It also means the compiler checks that the schema matches the
    /// type the handler takes.
    /// </para>
    /// <para>
    /// Validation is asynchronous, so a schema with a <c>RefineAsync</c> rule works here with no
    /// further ceremony, and the request's cancellation token is passed through to it.
    /// </para>
    /// <para>
    /// A failure produces a 400 with <c>ValidationProblemDetails</c>, whose errors are keyed by
    /// rendered path. The handler is not called.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly Schema&lt;CreateUser&gt; CreateUserSchema =
    ///     Theo.Object&lt;CreateUser&gt;()
    ///         .Field(x =&gt; x.Email, Theo.String().Trim().Email())
    ///         .Field(x =&gt; x.Age, Theo.Int().Min(18));
    ///
    /// app.MapPost("/users", (CreateUser request) =&gt; Results.Ok())
    ///    .Validate(CreateUserSchema);
    /// </code>
    /// </example>
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder, Schema<T> schema)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(schema);

        return builder
            .AddEndpointFilter(new ValidationEndpointFilter<T>(schema, options: null))
            .WithMetadata(TheonSchemaMetadata.For(schema))
            .ProducesValidationProblem();
    }

    /// <summary>
    /// Validates the endpoint's <typeparamref name="T"/> argument, answering the way
    /// <paramref name="configure"/> says.
    /// </summary>
    /// <typeparam name="T">The type to validate, which the handler must accept as a parameter.</typeparam>
    /// <param name="builder">The endpoint being built.</param>
    /// <param name="schema">The schema the argument must satisfy.</param>
    /// <param name="configure">Changes the response for this endpoint alone.</param>
    /// <remarks>
    /// For the endpoint that answers differently from the rest of the application. Where every
    /// endpoint answers the same way, <c>AddTheon</c> says it once and this overload is not needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.MapPost("/users", (CreateUser request) =&gt; Results.Ok())
    ///    .Validate(CreateUserSchema, options =&gt; options.Status = StatusCodes.Status422UnprocessableEntity);
    /// </code>
    /// </example>
    public static RouteHandlerBuilder Validate<T>(
        this RouteHandlerBuilder builder,
        Schema<T> schema,
        Action<TheonValidationOptions> configure)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(configure);

        return builder
            .AddEndpointFilter(new ValidationEndpointFilter<T>(schema, Configured(configure)))
            .WithMetadata(TheonSchemaMetadata.For(schema))
            .ProducesValidationProblem();
    }

    /// <summary>
    /// Validates the <typeparamref name="T"/> argument of every endpoint in this group.
    /// </summary>
    /// <typeparam name="T">The type to validate.</typeparam>
    /// <param name="builder">The group being built.</param>
    /// <param name="schema">The schema the argument must satisfy.</param>
    /// <remarks>
    /// Every endpoint in the group must accept a <typeparamref name="T"/>; one that does not will
    /// say so when it is called.
    /// </remarks>
    public static RouteGroupBuilder Validate<T>(this RouteGroupBuilder builder, Schema<T> schema)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(schema);

        builder.AddEndpointFilter(new ValidationEndpointFilter<T>(schema, options: null));
        builder.WithMetadata(TheonSchemaMetadata.For(schema));
        return builder;
    }

    /// <summary>
    /// Validates the <typeparamref name="T"/> argument of every endpoint in this group, answering the
    /// way <paramref name="configure"/> says.
    /// </summary>
    /// <typeparam name="T">The type to validate.</typeparam>
    /// <param name="builder">The group being built.</param>
    /// <param name="schema">The schema the argument must satisfy.</param>
    /// <param name="configure">Changes the response for this group alone.</param>
    public static RouteGroupBuilder Validate<T>(
        this RouteGroupBuilder builder,
        Schema<T> schema,
        Action<TheonValidationOptions> configure)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddEndpointFilter(new ValidationEndpointFilter<T>(schema, Configured(configure)));
        builder.WithMetadata(TheonSchemaMetadata.For(schema));
        return builder;
    }

    // Read once, while the endpoint is being built, rather than per request. An endpoint's answer is
    // not something that changes between calls.
    private static TheonValidationOptions Configured(Action<TheonValidationOptions> configure)
    {
        var options = new TheonValidationOptions();
        configure(options);
        return options;
    }
}
