using System.Globalization;

namespace Theon.Errors;

/// <summary>
/// The built-in English messages, used when no provider supplies one.
/// </summary>
/// <remarks>
/// These are deliberately terse and free of the offending value: a message is displayed to an end
/// user, and echoing back what they typed is both noisy and, for a password field, unsafe. The
/// value is available on <see cref="ValidationErrorInfo"/> for callers that want it.
/// </remarks>
public static class DefaultErrorMessages
{
    /// <summary>Returns the default English message for a validation failure.</summary>
    /// <param name="error">The structured facts about the failure.</param>
    public static string For(in ValidationErrorInfo error) => error.Code switch
    {
        ValidationErrorCode.InvalidType => InvalidType(error),
        ValidationErrorCode.TooSmall => TooSmall(error),
        ValidationErrorCode.TooBig => TooBig(error),
        ValidationErrorCode.InvalidFormat => InvalidFormat(error),
        ValidationErrorCode.NotMultipleOf => string.Create(
            CultureInfo.InvariantCulture,
            $"Must be a multiple of {error.Divisor}."),
        ValidationErrorCode.InvalidValue => "Invalid value.",
        ValidationErrorCode.NotEqual => string.Create(
            CultureInfo.InvariantCulture,
            $"Must be {error.Expected}."),
        ValidationErrorCode.Duplicate => "Already listed.",
        _ => "Invalid input.",
    };

    private static string InvalidType(in ValidationErrorInfo error)
    {
        if (error.Received is "null")
        {
            return "A value is required.";
        }

        return error.Expected is null
            ? "Invalid type."
            : string.Create(CultureInfo.InvariantCulture, $"Expected {error.Expected}.");
    }

    private static string TooSmall(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => string.Create(
            CultureInfo.InvariantCulture,
            $"Must be at least {error.Minimum} character(s) long."),
        ValidationOrigin.Collection => string.Create(
            CultureInfo.InvariantCulture,
            $"Must contain at least {error.Minimum} item(s)."),
        _ => error.Inclusive
            ? string.Create(CultureInfo.InvariantCulture, $"Must be greater than or equal to {error.Minimum}.")
            : string.Create(CultureInfo.InvariantCulture, $"Must be greater than {error.Minimum}."),
    };

    private static string TooBig(in ValidationErrorInfo error) => error.Origin switch
    {
        ValidationOrigin.Text => string.Create(
            CultureInfo.InvariantCulture,
            $"Must be at most {error.Maximum} character(s) long."),
        ValidationOrigin.Collection => string.Create(
            CultureInfo.InvariantCulture,
            $"Must contain at most {error.Maximum} item(s)."),
        _ => error.Inclusive
            ? string.Create(CultureInfo.InvariantCulture, $"Must be less than or equal to {error.Maximum}.")
            : string.Create(CultureInfo.InvariantCulture, $"Must be less than {error.Maximum}."),
    };

    private static string InvalidFormat(in ValidationErrorInfo error) => error.Format switch
    {
        "email" => "Invalid e-mail address.",
        "url" => "Invalid web address.",
        "uuid" => "Invalid UUID.",
        "base64" => "Invalid base64.",
        "base64url" => "Invalid URL-safe base64.",
        "hex" => "Must be hexadecimal.",
        // "Invalid E.164 number" would be accurate and useless: the person reading this filled in a
        // form and has never heard of E.164.
        "e164" => "Invalid phone number.",
        "iso8601" => "Invalid date and time.",
        "iso8601_date" => "Invalid date.",
        "absolute_uri" => "Must be an absolute address.",
        "uri_scheme" => string.Create(
            CultureInfo.InvariantCulture,
            $"Scheme must be one of: {error.Expected}."),
        "ipv4" => "Invalid IPv4 address.",
        "ipv6" => "Invalid IPv6 address.",
        "cidr" => "Invalid CIDR range.",
        "hostname" => "Invalid host name.",
        "jwt" => "Invalid token.",
        "credit_card" => "Invalid card number.",
        "iban" => "Invalid IBAN.",
        "regex" => "Invalid format.",
        "starts_with" => string.Create(CultureInfo.InvariantCulture, $"Must start with {error.Expected}."),
        "ends_with" => string.Create(CultureInfo.InvariantCulture, $"Must end with {error.Expected}."),
        "contains" => string.Create(CultureInfo.InvariantCulture, $"Must contain {error.Expected}."),
        "uppercase" => "Must not contain lowercase letters.",
        "lowercase" => "Must not contain uppercase letters.",
        null => "Invalid format.",
        _ => string.Create(CultureInfo.InvariantCulture, $"Invalid {error.Format}."),
    };
}
