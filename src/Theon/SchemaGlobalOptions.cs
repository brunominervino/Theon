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

    private static volatile System.Text.Json.JsonNamingPolicy? _propertyNamingPolicy;

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

    /// <summary>
    /// Gets or sets the policy a property name in an error path is renamed with. Leave it
    /// <see langword="null"/> to report the C# member name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An error path used to come out as the C# member name, which is the one name nobody on the wire
    /// uses: an application serializing camelCase returned <c>ZipCode</c> in the error and received
    /// <c>zipCode</c> in the body, so a form could not key one by the other.
    /// </para>
    /// <para>
    /// Read when <see cref="ParseOptions.PropertyNamingPolicy"/> is <see langword="null"/>, so one
    /// call can still choose its own. An index is a position and not a name, so a policy does nothing
    /// to it.
    /// </para>
    /// </remarks>
    public static System.Text.Json.JsonNamingPolicy? PropertyNamingPolicy
    {
        get => _propertyNamingPolicy;
        set => _propertyNamingPolicy = value;
    }
}
