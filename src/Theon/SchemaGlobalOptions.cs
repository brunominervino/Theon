using Theon.Errors;

namespace Theon;

/// <summary>
/// Process-wide defaults for every schema.
/// </summary>
/// <remarks>
/// Intended to be configured once at application start-up, before any parsing happens. Reads are
/// cheap and safe from any thread; a write that races with a parse in progress is not guaranteed
/// to be observed by that parse.
/// </remarks>
public static class SchemaGlobalOptions
{
    private static volatile SchemaErrorMessageProvider? _messageProvider;

    /// <summary>
    /// Gets or sets the provider consulted for every error message that no more specific provider
    /// has claimed. Leave it <see langword="null"/> to use the built-in English messages.
    /// </summary>
    /// <remarks>
    /// This is the extension point for localization: supply a provider that maps
    /// <see cref="ValidationErrorInfo.Code"/> and <see cref="ValidationErrorInfo.Origin"/> onto
    /// strings from the resource system, and return <see langword="null"/> for any case it does
    /// not handle so the default still applies.
    /// </remarks>
    public static SchemaErrorMessageProvider? MessageProvider
    {
        get => _messageProvider;
        set => _messageProvider = value;
    }
}
