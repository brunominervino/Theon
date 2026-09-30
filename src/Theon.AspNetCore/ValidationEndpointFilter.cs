using Microsoft.AspNetCore.Http;

namespace Theon.AspNetCore;

// Validates one argument of the endpoint before the handler runs.
//
// The schema is handed in rather than resolved from services: an endpoint says which schema
// guards it, in the same place it says what it accepts, and a reader can see both without
// going to look at a registration elsewhere.
internal sealed class ValidationEndpointFilter<T>(Schema<T> schema) : IEndpointFilter
    where T : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var argument = FindArgument(context);

        var result = await schema
            .SafeParseAsync(argument!, cancellationToken: context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return TypedResults.ValidationProblem(result.Errors.ToProblemDetailsErrors());
        }

        return await next(context).ConfigureAwait(false);
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
