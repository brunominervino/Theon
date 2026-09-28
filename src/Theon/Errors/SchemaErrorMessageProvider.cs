namespace Theon.Errors;

/// <summary>
/// Produces the human-readable message for a validation failure, or defers to the next provider.
/// </summary>
/// <param name="error">The structured facts about the failure.</param>
/// <returns>
/// The message to use, or <see langword="null"/> to defer to the next provider in the chain.
/// </returns>
/// <remarks>
/// Providers are consulted in order of increasing generality: the message attached to the
/// individual rule, then the provider on <see cref="ParseOptions"/>, then
/// <see cref="SchemaGlobalOptions.MessageProvider"/>, then the built-in English defaults. The first
/// one to return a non-<see langword="null"/> string wins, so a provider can localize the cases it
/// cares about and return <see langword="null"/> for the rest.
/// </remarks>
public delegate string? SchemaErrorMessageProvider(in ValidationErrorInfo error);
