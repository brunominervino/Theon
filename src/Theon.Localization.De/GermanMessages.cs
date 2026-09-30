using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>
/// Validation messages in German.
/// </summary>
/// <remarks>
/// <para>
/// A message provider, not a resource file. It reads the structured facts of a failure — the code,
/// what the bound was measured against, the bound itself — and writes a sentence. That is why the
/// core never needed to know about languages: it decides what went wrong, this decides how to say
/// it.
/// </para>
/// <para>
/// Messages address the reader formally, with <em>Sie</em>, which is what a form a stranger fills in
/// calls for.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For the whole application, once at start-up:
/// SchemaGlobalOptions.MessageProvider = GermanMessages.Provider;
///
/// // Or for one parse:
/// schema.SafeParse(value, new ParseOptions { MessageProvider = GermanMessages.Provider });
/// </code>
/// </example>
public static class GermanMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the German message for a failure.</summary>
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
        ValidationErrorCode.NotMultipleOf => Format($"Muss ein Vielfaches von {error.Divisor} sein."),
        ValidationErrorCode.InvalidValue => "Ungültiger Wert.",
        ValidationErrorCode.NotEqual => Format($"Muss {error.Expected} sein."),
        ValidationErrorCode.Duplicate => "Doppelter Eintrag.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Dieses Feld ist erforderlich.";
        }

        return error.Expected is null
            ? "Ungültiger Typ."
            : Format($"Erwartet: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        // No plural branch for characters, and not an oversight: "Zeichen" is the same word in the
        // singular and the plural, so a Plural call here would pick between two identical strings.
        ValidationOrigin.Text => Format($"Geben Sie mindestens {error.Minimum} Zeichen ein."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Geben Sie mindestens 1 Element an.",
            $"Geben Sie mindestens {error.Minimum} Elemente an."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Muss gleich oder nach {error.Minimum} liegen.")
            : Format($"Muss nach {error.Minimum} liegen."),
        _ => error.Inclusive
            ? Format($"Muss größer oder gleich {error.Minimum} sein.")
            : Format($"Muss größer als {error.Minimum} sein."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Format($"Geben Sie höchstens {error.Maximum} Zeichen ein."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Geben Sie höchstens 1 Element an.",
            $"Geben Sie höchstens {error.Maximum} Elemente an."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Muss gleich oder vor {error.Maximum} liegen.")
            : Format($"Muss vor {error.Maximum} liegen."),
        _ => error.Inclusive
            ? Format($"Muss kleiner oder gleich {error.Maximum} sein.")
            : Format($"Muss kleiner als {error.Maximum} sein."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Ungültige E-Mail-Adresse.",
        "url" => "Ungültige Webadresse.",
        "uuid" => "Ungültige UUID.",
        "base64" => "Ungültiges Base64.",
        "base64url" => "Ungültiges URL-sicheres Base64.",
        "hex" => "Muss hexadezimal sein.",
        "e164" => "Ungültige Telefonnummer.",
        "iso8601" => "Ungültige Datums- und Zeitangabe.",
        "iso8601_date" => "Ungültiges Datum.",
        "absolute_uri" => "Muss eine absolute Adresse sein.",
        "uri_scheme" => Format($"Zulässige Schemas: {error.Expected}."),
        "ipv4" => "Ungültige IPv4-Adresse.",
        "ipv6" => "Ungültige IPv6-Adresse.",
        "cidr" => "Ungültiger CIDR-Bereich.",
        "hostname" => "Ungültiger Hostname.",
        "jwt" => "Ungültiges Token.",
        "credit_card" => "Ungültige Kartennummer.",
        "iban" => "Ungültige IBAN.",
        "regex" => "Ungültiges Format.",
        "starts_with" => Format($"Muss mit {error.Expected} beginnen."),
        "ends_with" => Format($"Muss mit {error.Expected} enden."),
        "contains" => Format($"Muss {error.Expected} enthalten."),
        "uppercase" => "Darf keine Kleinbuchstaben enthalten.",
        "lowercase" => "Darf keine Großbuchstaben enthalten.",
        null => "Ungültiges Format.",
        _ => null,
    };

    // Getting the singular wrong is the tell of a translation that was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("de"));
}
