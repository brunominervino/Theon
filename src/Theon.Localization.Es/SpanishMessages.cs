using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>
/// Validation messages in Spanish.
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
/// SchemaGlobalOptions.MessageProvider = SpanishMessages.Provider;
///
/// // Or for one parse:
/// schema.SafeParse(value, new ParseOptions { MessageProvider = SpanishMessages.Provider });
/// </code>
/// </example>
public static class SpanishMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the Spanish message for a failure.</summary>
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
        ValidationErrorCode.NotMultipleOf => Format($"Debe ser múltiplo de {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Valor no válido.",
        ValidationErrorCode.NotEqual => Format($"Debe ser {error.Expected}."),
        ValidationErrorCode.Duplicate => "Elemento repetido.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Este campo es obligatorio.";
        }

        return error.Expected is null
            ? "Tipo no válido."
            : Format($"Se esperaba: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        // "carácter" carries an accent that "caracteres" loses. Two words, not one word and an s.
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Escriba al menos 1 carácter.",
            $"Escriba al menos {error.Minimum} caracteres."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Indique al menos 1 elemento.",
            $"Indique al menos {error.Minimum} elementos."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Debe ser igual o posterior a {error.Minimum}.")
            : Format($"Debe ser posterior a {error.Minimum}."),
        ValidationOrigin.Bytes => Plural(
            error.Minimum,
            "Debe ocupar al menos 1 byte.",
            $"Debe ocupar al menos {error.Minimum} bytes."),
        _ => error.Inclusive
            ? Format($"Debe ser mayor o igual que {error.Minimum}.")
            : Format($"Debe ser mayor que {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Escriba como máximo 1 carácter.",
            $"Escriba como máximo {error.Maximum} caracteres."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Indique como máximo 1 elemento.",
            $"Indique como máximo {error.Maximum} elementos."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Debe ser igual o anterior a {error.Maximum}.")
            : Format($"Debe ser anterior a {error.Maximum}."),
        ValidationOrigin.Bytes => Plural(
            error.Maximum,
            "Debe ocupar como máximo 1 byte.",
            $"Debe ocupar como máximo {error.Maximum} bytes."),
        _ => error.Inclusive
            ? Format($"Debe ser menor o igual que {error.Maximum}.")
            : Format($"Debe ser menor que {error.Maximum}."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Correo electrónico no válido.",
        "url" => "Dirección web no válida.",
        "uuid" => "UUID no válido.",
        "base64" => "Base64 no válido.",
        "base64url" => "Base64 para URL no válido.",
        "hex" => "Debe ser hexadecimal.",
        "e164" => "Teléfono no válido.",
        "iso8601" => "Fecha y hora no válidas.",
        "iso8601_date" => "Fecha no válida.",
        "absolute_uri" => "Debe ser una dirección absoluta.",
        "uri_scheme" => Format($"El esquema debe ser uno de: {error.Expected}."),
        "ipv4" => "Dirección IPv4 no válida.",
        "ipv6" => "Dirección IPv6 no válida.",
        "cidr" => "Rango CIDR no válido.",
        "hostname" => "Nombre de host no válido.",
        "jwt" => "Token no válido.",
        "credit_card" => "Número de tarjeta no válido.",
        "iban" => "IBAN no válido.",
        "iso8601_time" => "Hora no válida.",
        "iso8601_duration" => "Duración no válida.",
        "content_type" => Format($"El tipo debe ser uno de estos: {error.Expected}."),
        "file_extension" => Format($"La extensión debe ser una de estas: {error.Expected}."),
        "regex" => "Formato no válido.",
        "starts_with" => Format($"Debe empezar por {error.Expected}."),
        "ends_with" => Format($"Debe terminar en {error.Expected}."),
        "contains" => Format($"Debe contener {error.Expected}."),
        "uppercase" => "No puede contener minúsculas.",
        "lowercase" => "No puede contener mayúsculas.",
        null => "Formato no válido.",
        _ => null,
    };

    // Getting the singular wrong is the tell of a translation that was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("es"));
}
