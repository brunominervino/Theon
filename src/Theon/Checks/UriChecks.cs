using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

// Requires an absolute address.
// Aborts the remaining rules on failure, because nearly every other question about a Uri — its
// scheme, its host, its port — throws InvalidOperationException on a relative one. A rule that
// cannot be asked is not a rule that failed.
internal sealed class AbsoluteUriCheck : Check<Uri>
{
    internal override bool AbortsOnFailure => true;

    // A Uri schema describes itself as uri-reference, which is the format that admits a relative
    // address. Requiring an absolute one narrows that to uri.
    internal override void Describe(SchemaDescriptionBuilder description) => description.Format = "uri";

    internal override void Run(ref ParseContext context, ref Uri value)
    {
        if (value.IsAbsoluteUri)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Format = "absolute_uri",
            },
            Message);
    }
}

// Requires the address to use one of a set of schemes.
// A relative address fails this rule rather than throwing, so the rule is safe to write without
// remembering to put Absolute in front of it.
internal sealed class UriSchemeCheck(string[] schemes, string expected) : Check<Uri>
{
    internal override void Run(ref ParseContext context, ref Uri value)
    {
        if (value.IsAbsoluteUri && Array.IndexOf(schemes, value.Scheme) >= 0)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Format = "uri_scheme",
                Expected = expected,
            },
            Message);
    }
}
