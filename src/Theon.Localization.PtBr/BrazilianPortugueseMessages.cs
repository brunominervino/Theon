using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>
/// Validation messages in Brazilian Portuguese.
/// </summary>
/// <remarks>
/// <para>
/// A message provider, not a resource file. It reads the structured facts of a failure — the code,
/// what the bound was measured against, the bound itself — and writes a sentence. That is why the
/// core never needed to know about languages: it decides what went wrong, this decides how to say
/// it.
/// </para>
/// <para>
/// Messages are addressed to the person filling in the form, in the imperative, without echoing
/// the value they typed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // For the whole application, once at start-up:
/// SchemaGlobalOptions.MessageProvider = BrazilianPortugueseMessages.Provider;
///
/// // Or for one parse:
/// schema.SafeParse(value, new ParseOptions { MessageProvider = BrazilianPortugueseMessages.Provider });
/// </code>
/// </example>
public static class BrazilianPortugueseMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the Brazilian Portuguese message for a failure.</summary>
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
        ValidationErrorCode.NotMultipleOf => Format($"Deve ser múltiplo de {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Valor inválido.",
        ValidationErrorCode.NotEqual => Format($"Deve ser {error.Expected}."),
        ValidationErrorCode.Duplicate => "Item repetido.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "Campo obrigatório.";
        }

        return error.Expected is null
            ? "Tipo inválido."
            : Format($"Esperado: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Informe pelo menos 1 caractere.",
            $"Informe pelo menos {error.Minimum} caracteres."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Informe pelo menos 1 item.",
            $"Informe pelo menos {error.Minimum} itens."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Deve ser igual ou posterior a {error.Minimum}.")
            : Format($"Deve ser posterior a {error.Minimum}."),
        _ => error.Inclusive
            ? Format($"Deve ser maior ou igual a {error.Minimum}.")
            : Format($"Deve ser maior que {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Informe no máximo 1 caractere.",
            $"Informe no máximo {error.Maximum} caracteres."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Informe no máximo 1 item.",
            $"Informe no máximo {error.Maximum} itens."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Deve ser igual ou anterior a {error.Maximum}.")
            : Format($"Deve ser anterior a {error.Maximum}."),
        _ => error.Inclusive
            ? Format($"Deve ser menor ou igual a {error.Maximum}.")
            : Format($"Deve ser menor que {error.Maximum}."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "E-mail inválido.",
        "url" => "Endereço web inválido.",
        "uuid" => "UUID inválido.",
        "base64" => "Base64 inválido.",
        "base64url" => "Base64 para URL inválido.",
        "hex" => "Deve ser hexadecimal.",
        // "Número E.164 inválido" seria exato e inútil: quem lê isto preencheu um formulário e
        // nunca ouviu falar de E.164.
        "e164" => "Telefone inválido.",
        "iso8601" => "Data e hora inválidas.",
        "iso8601_date" => "Data inválida.",
        "absolute_uri" => "Deve ser um endereço absoluto.",
        "uri_scheme" => Format($"O esquema deve ser um destes: {error.Expected}."),
        "ipv4" => "Endereço IPv4 inválido.",
        "ipv6" => "Endereço IPv6 inválido.",
        "cidr" => "Faixa CIDR inválida.",
        "hostname" => "Nome de host inválido.",
        "jwt" => "Token inválido.",
        "credit_card" => "Número de cartão inválido.",
        "iban" => "IBAN inválido.",
        "regex" => "Formato inválido.",
        "starts_with" => Format($"Deve começar com {error.Expected}."),
        "ends_with" => Format($"Deve terminar com {error.Expected}."),
        "contains" => Format($"Deve conter {error.Expected}."),
        "uppercase" => "Não pode conter letras minúsculas.",
        "lowercase" => "Não pode conter letras maiúsculas.",
        null => "Formato inválido.",
        _ => null,
    };

    // "1 caractere" against "2 caracteres": getting this wrong is the tell of a translation that
    // was pasted rather than written.
    private static string Plural(object? bound, string singular, string plural) =>
        bound is 1 ? singular : plural;

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("pt-BR"));
}
