using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Theon.Errors;

namespace Theon.AspNetCore;

// Validates one argument of the endpoint before the handler runs.
//
// The schema is handed in rather than resolved from services: an endpoint says which schema
// guards it, in the same place it says what it accepts, and a reader can see both without
// going to look at a registration elsewhere.
internal sealed class ValidationEndpointFilter<T>(Schema<T> schema, TheonValidationOptions? options)
    : IEndpointFilter
    where T : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Named at the endpoint if it was named there; otherwise whatever AddTheon registered; and
        // otherwise the defaults, so an application that configures nothing still works.
        var settings = options
            ?? context.HttpContext.RequestServices
                .GetService<IOptions<TheonValidationOptions>>()?.Value
            ?? TheonValidationOptions.Default;

        var argument = FindArgument(context);

        var result = await schema
            .SafeParseAsync(
                argument!,
                new ParseOptions { PropertyNamingPolicy = settings.PropertyNamingPolicy },
                context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return TypedResults.Problem(Problem(result.Errors, settings));
        }

        return await next(context).ConfigureAwait(false);
    }

    // Two shapes, because adding the codes changes the shape of errors and that is a break for
    // anything reading the response. The default is the one this package always answered with.
    private static ProblemDetails Problem(
        IReadOnlyList<ValidationError> errors,
        TheonValidationOptions settings)
    {
        ProblemDetails problem = settings.IncludeErrorCodes
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(errors.ToProblemDetailsErrors());

        problem.Status = settings.Status;

        if (settings.Type is not null)
        {
            problem.Type = settings.Type;
        }

        if (settings.Title is not null)
        {
            problem.Title = settings.Title;
        }

        if (settings.Code is not null)
        {
            problem.Extensions["code"] = settings.Code;
        }

        if (settings.IncludeErrorCodes)
        {
            problem.Extensions["errors"] = Detailed(errors);
        }

        return problem;
    }

    // Grouped by path, like the dictionary the default shape carries, so a client reading one can
    // read the other by looking at an entry rather than at the whole document.
    private static Dictionary<string, List<ValidationErrorDetail>> Detailed(
        IReadOnlyList<ValidationError> errors)
    {
        var detailed = new Dictionary<string, List<ValidationErrorDetail>>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            var key = error.Path.ToString();

            if (!detailed.TryGetValue(key, out var entries))
            {
                entries = [];
                detailed[key] = entries;
            }

            entries.Add(new ValidationErrorDetail(
                error.Message,
                error.CustomCode ?? error.Code.ToString()));
        }

        return detailed;
    }

    private static T? FindArgument(EndpointFilterInvocationContext context)
    {
        var sawNull = false;

        foreach (var argument in context.Arguments)
        {
            if (argument is T typed)
            {
                return typed;
            }

            sawNull |= argument is null;
        }

        // A null argument carries no type, so a missing body is indistinguishable from an endpoint
        // that never had a parameter of this type. Passing the null on lets the schema reject it,
        // which is the right answer for a missing body. With no null to blame, the endpoint simply
        // has no such parameter, and that is a wiring mistake worth saying out loud.
        if (!sawNull)
        {
            throw new InvalidOperationException(
                $"This endpoint has no argument of type {typeof(T).Name}, so there is nothing for " +
                "the validation filter to check. Validate the type the handler actually accepts.");
        }

        return default;
    }
}
