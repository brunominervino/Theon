using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>Validation messages in Dutch.</summary>
/// <remarks>
/// A message provider, not a resource file. It reads the structured facts of a failure and writes a
/// sentence, which is why the core never needed to know about languages.
/// </remarks>
/// <example>
/// <code>
/// SchemaGlobalOptions.MessageProvider = DutchMessages.Provider;
/// </code>
/// </example>
public static class DutchMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the Dutch message for a failure.</summary>
    /// <param name="error">The structured facts about the failure.</param>
    /// <returns>The message, or <see langword="null"/> for a failure this provider does not describe.</returns>
    public static string? For(in ValidationErrorInfo error) => error.Code switch
    {
        ValidationErrorCode.InvalidType => InvalidType(error),
        ValidationErrorCode.TooSmall => TooSmall(error),
        ValidationErrorCode.TooBig => TooBig(error),
        ValidationErrorCode.InvalidFormat => InvalidFormat(error),
        ValidationErrorCode.NotMultipleOf => Format($"Moet een veelvoud van {error.Divisor} zijn."),
        ValidationErrorCode.InvalidValue => "Ongeldige waarde.",
        ValidationErrorCode.NotEqual => Format($"Moet {error.Expected} zijn."),
        ValidationErrorCode.Duplicate => "Dubbel item.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Dit veld is verplicht.";
        }

        return error.Expected is null
            ? "Ongeldig type."
            : Format($"Verwacht: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Voer minstens 1 teken in.",
            $"Voer minstens {error.Minimum} tekens in."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Geef minstens 1 item op.",
            $"Geef minstens {error.Minimum} items op."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Moet gelijk zijn aan of na {error.Minimum} liggen.")
            : Format($"Moet na {error.Minimum} liggen."),
        ValidationOrigin.Bytes => Plural(
            error.Minimum,
            "Moet minstens 1 byte groot zijn.",
            $"Moet minstens {error.Minimum} bytes groot zijn."),
        _ => error.Inclusive
            ? Format($"Moet groter dan of gelijk aan {error.Minimum} zijn.")
            : Format($"Moet groter dan {error.Minimum} zijn."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Voer maximaal 1 teken in.",
            $"Voer maximaal {error.Maximum} tekens in."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Geef maximaal 1 item op.",
            $"Geef maximaal {error.Maximum} items op."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Moet gelijk zijn aan of voor {error.Maximum} liggen.")
            : Format($"Moet voor {error.Maximum} liggen."),
        ValidationOrigin.Bytes => Plural(
            error.Maximum,
            "Mag maximaal 1 byte groot zijn.",
            $"Mag maximaal {error.Maximum} bytes groot zijn."),
        _ => error.Inclusive
            ? Format($"Moet kleiner dan of gelijk aan {error.Maximum} zijn.")
            : Format($"Moet kleiner dan {error.Maximum} zijn."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Ongeldig e-mailadres.",
        "url" => "Ongeldig webadres.",
        "uuid" => "Ongeldige UUID.",
        "base64" => "Ongeldige base64.",
        "base64url" => "Ongeldige URL-veilige base64.",
        "hex" => "Moet hexadecimaal zijn.",
        "e164" => "Ongeldig telefoonnummer.",
        "iso8601" => "Ongeldige datum en tijd.",
        "iso8601_date" => "Ongeldige datum.",
        "absolute_uri" => "Moet een absoluut adres zijn.",
        "uri_scheme" => Format($"Schema moet een van de volgende zijn: {error.Expected}."),
        "ipv4" => "Ongeldig IPv4-adres.",
        "ipv6" => "Ongeldig IPv6-adres.",
        "cidr" => "Ongeldig CIDR-bereik.",
        "hostname" => "Ongeldige hostnaam.",
        "jwt" => "Ongeldig token.",
        "credit_card" => "Ongeldig kaartnummer.",
        "iban" => "Ongeldige IBAN.",
        "iso8601_time" => "Ongeldige tijd.",
        "iso8601_duration" => "Ongeldige duur.",
        "content_type" => Format($"Het type moet een van deze zijn: {error.Expected}."),
        "file_extension" => Format($"De bestandsextensie moet een van deze zijn: {error.Expected}."),
        "regex" => "Ongeldige indeling.",
        "starts_with" => Format($"Moet beginnen met {error.Expected}."),
        "ends_with" => Format($"Moet eindigen op {error.Expected}."),
        "contains" => Format($"Moet {error.Expected} bevatten."),
        "uppercase" => "Mag geen kleine letters bevatten.",
        "lowercase" => "Mag geen hoofdletters bevatten.",
        null => "Ongeldige indeling.",
        _ => null,
    };

    // Getting the singular wrong is the tell of a translation that was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("nl"));
}
