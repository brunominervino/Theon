using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>
/// Validation messages in French.
/// </summary>
/// <remarks>
/// <para>
/// A message provider, not a resource file. It reads the structured facts of a failure — the code,
/// what the bound was measured against, the bound itself — and writes a sentence. That is why the
/// core never needed to know about languages: it decides what went wrong, this decides how to say
/// it.
/// </para>
/// <para>
/// Messages are addressed to the person filling in the form, without echoing the value they typed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For the whole application, once at start-up:
/// SchemaGlobalOptions.MessageProvider = FrenchMessages.Provider;
///
/// // Or for one parse:
/// schema.SafeParse(value, new ParseOptions { MessageProvider = FrenchMessages.Provider });
/// </code>
/// </example>
public static class FrenchMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the French message for a failure.</summary>
    /// <param name="error">The structured facts about the failure.</param>
    /// <returns>
    /// The message, or <see langword="null"/> for a failure this provider does not describe, which
    /// lets the next provider in the chain answer.
    /// </returns>
    public static string? For(in ValidationErrorInfo error) => error.Code switch
    {
        ValidationErrorCode.InvalidType => InvalidType(error),
        ValidationErrorCode.TooSmall => TooSmall(error),
        ValidationErrorCode.TooBig => TooBig(error),
        ValidationErrorCode.InvalidFormat => InvalidFormat(error),
        ValidationErrorCode.NotMultipleOf => Format($"Doit être un multiple de {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Valeur non valide.",
        ValidationErrorCode.NotEqual => Format($"Doit être {error.Expected}."),
        ValidationErrorCode.Duplicate => "Élément en doublon.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Ce champ est obligatoire.";
        }

        // A no-break space before the colon, which is what French typography asks for and what a
        // reader notices the absence of.
        return error.Expected is null
            ? "Type non valide."
            : Format($"Type attendu : {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Saisissez au moins 1 caractère.",
            $"Saisissez au moins {error.Minimum} caractères."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Indiquez au moins 1 élément.",
            $"Indiquez au moins {error.Minimum} éléments."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Doit être égal ou postérieur à {error.Minimum}.")
            : Format($"Doit être postérieur à {error.Minimum}."),
        _ => error.Inclusive
            ? Format($"Doit être supérieur ou égal à {error.Minimum}.")
            : Format($"Doit être supérieur à {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Saisissez au maximum 1 caractère.",
            $"Saisissez au maximum {error.Maximum} caractères."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Indiquez au maximum 1 élément.",
            $"Indiquez au maximum {error.Maximum} éléments."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Doit être égal ou antérieur à {error.Maximum}.")
            : Format($"Doit être antérieur à {error.Maximum}."),
        _ => error.Inclusive
            ? Format($"Doit être inférieur ou égal à {error.Maximum}.")
            : Format($"Doit être inférieur à {error.Maximum}."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Adresse e-mail non valide.",
        "url" => "Adresse web non valide.",
        "uuid" => "UUID non valide.",
        "base64" => "Base64 non valide.",
        "base64url" => "Base64 pour URL non valide.",
        "hex" => "Doit être hexadécimal.",
        "e164" => "Numéro de téléphone non valide.",
        "iso8601" => "Date et heure non valides.",
        "iso8601_date" => "Date non valide.",
        "absolute_uri" => "Doit être une adresse absolue.",
        "uri_scheme" => Format($"Le schéma doit être l’un de : {error.Expected}."),
        "ipv4" => "Adresse IPv4 non valide.",
        "ipv6" => "Adresse IPv6 non valide.",
        "cidr" => "Plage CIDR non valide.",
        "hostname" => "Nom d’hôte non valide.",
        "jwt" => "Jeton non valide.",
        "credit_card" => "Numéro de carte non valide.",
        "iban" => "IBAN non valide.",
        "regex" => "Format non valide.",
        "starts_with" => Format($"Doit commencer par {error.Expected}."),
        "ends_with" => Format($"Doit se terminer par {error.Expected}."),
        "contains" => Format($"Doit contenir {error.Expected}."),
        "uppercase" => "Ne doit pas contenir de minuscules.",
        "lowercase" => "Ne doit pas contenir de majuscules.",
        null => "Format non valide.",
        _ => null,
    };

    // French keeps the singular for one, like English. Getting it wrong is the tell of a translation
    // that was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("fr"));
}
