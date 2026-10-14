namespace Theon.Errors;

/// <summary>Combines message providers.</summary>
public static class MessageProviders
{
    /// <summary>Asks each provider in turn and takes the first message one of them writes.</summary>
    /// <param name="providers">The providers, in the order they should be asked.</param>
    /// <returns>A provider that delegates to them.</returns>
    /// <remarks>
    /// <para>
    /// A provider returning <see langword="null"/> declines, which is what lets one localize the cases
    /// it cares about and ignore the rest. That was already true of a single provider falling through
    /// to the built-in English; what was missing was any way to put two providers in that order.
    /// </para>
    /// <para>
    /// The typical shape is the application's own provider first and a language package behind it: the
    /// application names its own rules, the package says everything else, and the built-in English
    /// catches whatever neither answered.
    /// </para>
    /// <para>
    /// This does not change the chain itself. A message attached to an individual rule still wins over
    /// every provider here, and whatever is left unanswered still falls through to the provider on
    /// <see cref="SchemaGlobalOptions"/> and then to the English defaults.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// SchemaGlobalOptions.MessageProvider = MessageProviders.Chain(
    ///     NucleoMessages.Provider,                 // names this application's own rules
    ///     BrazilianPortugueseMessages.Provider);   // says everything else in Portuguese
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// <paramref name="providers"/> was empty, or one of them was <see langword="null"/>.
    /// </exception>
    public static SchemaErrorMessageProvider Chain(params SchemaErrorMessageProvider[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (providers.Length == 0)
        {
            throw new ArgumentException(
                "Name at least one provider. A chain of none answers nothing, which is what passing " +
                "no provider at all already does.",
                nameof(providers));
        }

        // Copied, so that a caller who keeps the array and changes it afterwards cannot change this
        // chain. The same reasoning as every other array this library is handed.
        var ordered = new SchemaErrorMessageProvider[providers.Length];

        for (var i = 0; i < providers.Length; i++)
        {
            ordered[i] = providers[i]
                ?? throw new ArgumentException(
                    "One of the providers is null. A chain with a hole in it would throw on the first " +
                    "error rather than at the line that built it.",
                    nameof(providers));
        }

        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        return (in ValidationErrorInfo error) =>
        {
            foreach (var provider in ordered)
            {
                if (provider(in error) is { } message)
                {
                    return message;
                }
            }

            return null;
        };
    }
}
