using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

internal sealed class Ipv4Check : Check<string>
{
    internal override void Describe(SchemaDescriptionBuilder description) => description.Format = "ipv4";

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (IpText.IsIpv4(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = "ipv4",
            },
            Message);
    }
}

internal sealed class Ipv6Check : Check<string>
{
    internal override void Describe(SchemaDescriptionBuilder description) => description.Format = "ipv6";

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (IpText.IsIpv6(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = "ipv6",
            },
            Message);
    }
}

// An address and a prefix length, in either family.
// Split on the slash by slicing rather than by Split, so the two halves cost nothing, and reuse the
// same scanners the two address rules use. A pattern would have had to restate both of them.
internal sealed class CidrCheck : Check<string>
{
    internal override void Describe(SchemaDescriptionBuilder description)
    {
        // Not a format the dialect knows, so this is an annotation rather than something a validator
        // will check -- which is exactly what format means in 2020-12 unless asked otherwise. Recorded
        // as unrepresentable as well, because a reader asking what will actually be enforced deserves
        // to hear that this will not be.
        description.Format = "cidr";
        description.CannotRepresent("Cidr");
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (IsCidr(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = "cidr",
            },
            Message);
    }

    private static bool IsCidr(ReadOnlySpan<char> value)
    {
        var slash = value.IndexOf('/');
        if (slash < 0)
        {
            return false;
        }

        var address = value[..slash];
        var prefix = value[(slash + 1)..];

        return IpText.IsIpv4(address)
            ? IpText.IsPrefixLength(prefix, 32)
            : IpText.IsIpv6(address) && IpText.IsPrefixLength(prefix, 128);
    }
}
