using Microsoft.Extensions.DependencyInjection;

namespace Theon.AspNetCore;

/// <summary>Configures what a validation failure answers with, for a whole application.</summary>
public static class TheonServiceCollectionExtensions
{
    /// <summary>Registers the validation options every <c>Validate</c> reads.</summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configure">Changes the defaults. Omit it to keep them.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <remarks>
    /// Calling this is optional. An endpoint that finds no options configured uses the same defaults
    /// this package has always answered with, so nothing has to be registered for
    /// <c>Validate</c> to work.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddTheon(options =&gt;
    /// {
    ///     options.Status = StatusCodes.Status422UnprocessableEntity;
    ///     options.Code = "validacao";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddTheon(
        this IServiceCollection services,
        Action<TheonValidationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<TheonValidationOptions>();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        return services;
    }
}
