using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>Validation messages in Italian.</summary>
/// <remarks>
/// A message provider, not a resource file. It reads the structured facts of a failure and writes a
/// sentence, which is why the core never needed to know about languages.
/// </remarks>
/// <example>
/// <code>
/// SchemaGlobalOptions.MessageProvider = ItalianMessages.Provider;
/// </code>
/// </example>
public static class ItalianMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the Italian message for a failure.</summary>
    /// <param name="error">The structured facts about the failure.</param>
    /// <returns>The message, or <see langword="null"/> for a failure this provider does not describe.</returns>
    public static string? For(in ValidationErrorInfo error) => error.Code switch
    {
        ValidationErrorCode.InvalidType => InvalidType(error),
        ValidationErrorCode.TooSmall => TooSmall(error),
        ValidationErrorCode.TooBig => TooBig(error),
        ValidationErrorCode.InvalidFormat => InvalidFormat(error),
        ValidationErrorCode.NotMultipleOf => Format($"Deve essere un multiplo di {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Valore non valido.",
        ValidationErrorCode.NotEqual => Format($"Deve essere {error.Expected}."),
        ValidationErrorCode.Duplicate => "Elemento duplicato.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Questo campo è obbligatorio.";
        }

        return error.Expected is null
            ? "Tipo non valido."
            : Format($"Previsto: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Inserisci almeno 1 carattere.",
            $"Inserisci almeno {error.Minimum} caratteri."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Indica almeno 1 elemento.",
            $"Indica almeno {error.Minimum} elementi."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Deve essere uguale o successivo a {error.Minimum}.")
            : Format($"Deve essere successivo a {error.Minimum}."),
        // No plural branch, and not an oversight: "byte" is invariable in Italian, so a Plural
        // call here would pick between two identical strings.
        ValidationOrigin.Bytes => Format($"Deve occupare almeno {error.Minimum} byte."),
        _ => error.Inclusive
            ? Format($"Deve essere maggiore o uguale a {error.Minimum}.")
            : Format($"Deve essere maggiore di {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Inserisci al massimo 1 carattere.",
            $"Inserisci al massimo {error.Maximum} caratteri."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Indica al massimo 1 elemento.",
            $"Indica al massimo {error.Maximum} elementi."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Deve essere uguale o precedente a {error.Maximum}.")
            : Format($"Deve essere precedente a {error.Maximum}."),
        ValidationOrigin.Bytes => Format($"Deve occupare al massimo {error.Maximum} byte."),
        _ => error.Inclusive
            ? Format($"Deve essere minore o uguale a {error.Maximum}.")
            : Format($"Deve essere minore di {error.Maximum}."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Indirizzo e-mail non valido.",
        "url" => "Indirizzo web non valido.",
        "uuid" => "UUID non valido.",
        "base64" => "Base64 non valido.",
        "base64url" => "Base64 per URL non valido.",
        "hex" => "Deve essere esadecimale.",
        "e164" => "Numero di telefono non valido.",
        "iso8601" => "Data e ora non valide.",
        "iso8601_date" => "Data non valida.",
        "absolute_uri" => "Deve essere un indirizzo assoluto.",
        "uri_scheme" => Format($"Lo schema deve essere uno di: {error.Expected}."),
        "ipv4" => "Indirizzo IPv4 non valido.",
        "ipv6" => "Indirizzo IPv6 non valido.",
        "cidr" => "Intervallo CIDR non valido.",
        "hostname" => "Nome host non valido.",
        "jwt" => "Token non valido.",
        "credit_card" => "Numero di carta non valido.",
        "iban" => "IBAN non valido.",
        "iso8601_time" => "Ora non valida.",
        "iso8601_duration" => "Durata non valida.",
        "content_type" => Format($"Il tipo deve essere uno di questi: {error.Expected}."),
        "file_extension" => Format($"L'estensione deve essere una di queste: {error.Expected}."),
        "regex" => "Formato non valido.",
        "starts_with" => Format($"Deve iniziare con {error.Expected}."),
        "ends_with" => Format($"Deve terminare con {error.Expected}."),
        "contains" => Format($"Deve contenere {error.Expected}."),
        "uppercase" => "Non può contenere minuscole.",
        "lowercase" => "Non può contenere maiuscole.",
        null => "Formato non valido.",
        _ => null,
    };

    // Getting the singular wrong is the tell of a translation that was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("it"));
}
