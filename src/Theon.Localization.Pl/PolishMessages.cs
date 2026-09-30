using System.Globalization;
using Theon.Errors;

namespace Theon.Localization;

/// <summary>Validation messages in Polish.</summary>
/// <remarks>
/// <para>
/// A message provider, not a resource file. It reads the structured facts of a failure and writes a
/// sentence, which is why the core never needed to know about languages.
/// </para>
/// <para>
/// This is the provider that shows what the mechanism is worth. Polish has three plural forms rather
/// than two, and which one a number takes depends on its last two digits, so the singular-and-plural
/// pair every other provider uses cannot express it. Nothing in the core had to change for that: a
/// provider is a function from facts to a sentence, and how it decides is entirely its own business.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// SchemaGlobalOptions.MessageProvider = PolishMessages.Provider;
/// </code>
/// </example>
public static class PolishMessages
{
    /// <summary>Gets the provider, ready to be assigned.</summary>
    public static SchemaErrorMessageProvider Provider { get; } = For;

    /// <summary>Returns the Polish message for a failure.</summary>
    /// <param name="error">The structured facts about the failure.</param>
    /// <returns>The message, or <see langword="null"/> for a failure this provider does not describe.</returns>
    public static string? For(in ValidationErrorInfo error) => error.Code switch
    {
        ValidationErrorCode.InvalidType => InvalidType(error),
        ValidationErrorCode.TooSmall => TooSmall(error),
        ValidationErrorCode.TooBig => TooBig(error),
        ValidationErrorCode.InvalidFormat => InvalidFormat(error),
        ValidationErrorCode.NotMultipleOf =>
            Format($"Musi być wielokrotnością {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Nieprawidłowa wartość.",
        ValidationErrorCode.NotEqual => Format($"Musi być {error.Expected}."),
        ValidationErrorCode.Duplicate => "Zduplikowany element.",
        ValidationErrorCode.Custom => null,
        _ => null,
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "To pole jest wymagane.";
        }

        return error.Expected is null
            ? "Nieprawidłowy typ."
            : Format($"Oczekiwano: {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Minimum,
            "Wpisz co najmniej 1 znak.",
            $"Wpisz co najmniej {error.Minimum} znaki.",
            $"Wpisz co najmniej {error.Minimum} znaków."),
        ValidationOrigin.Collection => Plural(
            error.Minimum,
            "Podaj co najmniej 1 element.",
            $"Podaj co najmniej {error.Minimum} elementy.",
            $"Podaj co najmniej {error.Minimum} elementów."),
        // The subject is named rather than left out, because the adjective has to agree with its
        // gender and "data" and "wartość" do not take the same ending as each other.
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Data musi być równa lub późniejsza niż {error.Minimum}.")
            : Format($"Data musi być późniejsza niż {error.Minimum}."),
        _ => error.Inclusive
            ? Format($"Wartość musi być większa lub równa {error.Minimum}.")
            : Format($"Wartość musi być większa niż {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => Plural(
            error.Maximum,
            "Wpisz najwyżej 1 znak.",
            $"Wpisz najwyżej {error.Maximum} znaki.",
            $"Wpisz najwyżej {error.Maximum} znaków."),
        ValidationOrigin.Collection => Plural(
            error.Maximum,
            "Podaj najwyżej 1 element.",
            $"Podaj najwyżej {error.Maximum} elementy.",
            $"Podaj najwyżej {error.Maximum} elementów."),
        ValidationOrigin.DateTime => error.Inclusive
            ? Format($"Data musi być równa lub wcześniejsza niż {error.Maximum}.")
            : Format($"Data musi być wcześniejsza niż {error.Maximum}."),
        _ => error.Inclusive
            ? Format($"Wartość musi być mniejsza lub równa {error.Maximum}.")
            : Format($"Wartość musi być mniejsza niż {error.Maximum}."),
    };

    private static string? InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Nieprawidłowy adres e-mail.",
        "url" => "Nieprawidłowy adres internetowy.",
        "uuid" => "Nieprawidłowy UUID.",
        "base64" => "Nieprawidłowy base64.",
        "base64url" => "Nieprawidłowy base64 dla adresu URL.",
        "hex" => "Musi być szesnastkowa.",
        "e164" => "Nieprawidłowy numer telefonu.",
        "iso8601" => "Nieprawidłowa data i godzina.",
        "iso8601_date" => "Nieprawidłowa data.",
        "absolute_uri" => "Musi być adresem bezwzględnym.",
        "uri_scheme" => Format($"Schemat musi być jednym z: {error.Expected}."),
        "ipv4" => "Nieprawidłowy adres IPv4.",
        "ipv6" => "Nieprawidłowy adres IPv6.",
        "cidr" => "Nieprawidłowy zakres CIDR.",
        "hostname" => "Nieprawidłowa nazwa hosta.",
        "jwt" => "Nieprawidłowy token.",
        "credit_card" => "Nieprawidłowy numer karty.",
        "iban" => "Nieprawidłowy IBAN.",
        "regex" => "Nieprawidłowy format.",
        "starts_with" => Format($"Musi zaczynać się od {error.Expected}."),
        "ends_with" => Format($"Musi kończyć się na {error.Expected}."),
        "contains" => Format($"Musi zawierać {error.Expected}."),
        "uppercase" => "Nie może zawierać małych liter.",
        "lowercase" => "Nie może zawierać wielkich liter.",
        null => "Nieprawidłowy format.",
        _ => null,
    };

    // One, a few, or many -- and which of the three depends on the last two digits, not just the last
    // one. Twelve takes the many form while twenty-two takes the few form, which is the rule a
    // two-way singular-and-plural helper cannot express and the reason this one has three arms.
    private static string Plural(object? bound, string one, string few, string many)
    {
        if (bound is not int count)
        {
            return many;
        }

        if (count == 1)
        {
            return one;
        }

        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;

        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? few : many;
    }

    private static string Format(FormattableString text) =>
        text.ToString(CultureInfo.GetCultureInfo("pl"));
}
