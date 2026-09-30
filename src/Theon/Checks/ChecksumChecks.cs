using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

// A card number, by the Luhn checksum rather than by shape.
//
// A pattern can say "twelve to nineteen digits" and that is the easy half. The checksum is what
// catches the single mistyped digit and the two transposed ones, which is what people actually do,
// and no pattern can compute it.
internal sealed class CreditCardCheck : Check<string>
{
    internal override void Describe(SchemaDescription description)
    {
        description.Format = "credit-card";
        description.CannotRepresent("CreditCard");
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (IsLuhnValid(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = "credit_card",
            },
            Message);
    }

    private static bool IsLuhnValid(ReadOnlySpan<char> value)
    {
        if (value.Length is < 12 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubling = false;

        for (var i = value.Length - 1; i >= 0; i--)
        {
            var digit = value[i] - '0';
            if ((uint)digit > 9)
            {
                return false;
            }

            if (doubling)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubling = !doubling;
        }

        return sum % 10 == 0;
    }
}

// An IBAN, by the mod-97 checksum of ISO 13616.
//
// The remainder is computed over the value rotated four characters to the left, and the rotated string
// is never built: the loop reads through the rotation instead. That keeps a valid account number free
// of allocation, which a Substring and a concatenation would not.
internal sealed class IbanCheck : Check<string>
{
    internal override void Describe(SchemaDescription description)
    {
        description.Format = "iban";
        description.CannotRepresent("Iban");
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (IsMod97Valid(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = "iban",
            },
            Message);
    }

    private static bool IsMod97Valid(ReadOnlySpan<char> value)
    {
        if (value.Length is < 15 or > 34)
        {
            return false;
        }

        if (!char.IsAsciiLetterUpper(value[0]) || !char.IsAsciiLetterUpper(value[1]) ||
            !char.IsAsciiDigit(value[2]) || !char.IsAsciiDigit(value[3]))
        {
            return false;
        }

        var remainder = 0;

        for (var offset = 0; offset < value.Length; offset++)
        {
            var c = value[(offset + 4) % value.Length];

            if (char.IsAsciiDigit(c))
            {
                remainder = ((remainder * 10) + (c - '0')) % 97;
            }
            else if (char.IsAsciiLetterUpper(c))
            {
                // A letter counts as its two-digit position in the alphabet, offset by ten.
                remainder = ((remainder * 100) + (c - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }
}
