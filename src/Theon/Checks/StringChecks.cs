using System.Globalization;
using System.Text.RegularExpressions;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

internal sealed class MinLengthCheck(int minimum) : Check<string>
{
    internal override void Describe(SchemaDescription description) =>
        description.MinLength = minimum;

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (!StringMeasure.IsShorterThan(value, minimum))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Text,
                Minimum = minimum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class MaxLengthCheck(int maximum) : Check<string>
{
    internal override void Describe(SchemaDescription description) =>
        description.MaxLength = maximum;

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (!StringMeasure.IsLongerThan(value, maximum))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Text,
                Maximum = maximum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class ExactLengthCheck(int length) : Check<string>
{
    internal override void Describe(SchemaDescription description)
    {
        description.MinLength = length;
        description.MaxLength = length;
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        var comparison = StringMeasure.CompareLength(value, length);
        if (comparison == 0)
        {
            return;
        }

        context.AddError(
            comparison < 0
                ? new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Text,
                    Minimum = length,
                    Inclusive = true,
                }
                : new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Text,
                    Maximum = length,
                    Inclusive = true,
                },
            Message);
    }
}

// A check that rewrites the value instead of rejecting it.
internal sealed class OverwriteCheck(Func<string, string> transform) : Check<string>
{
    internal override void Run(ref ParseContext context, ref string value) => value = transform(value);
}

internal sealed class PatternCheck(Regex pattern, string format) : Check<string>
{
    // A format this library names and JSON Schema also names becomes that format. Anything
    // else becomes the pattern itself, which is exact and needs no agreement between the two
    // vocabularies. Only one of the two is written: a format and a pattern side by side invite
    // a reader to wonder which one wins.
    internal override void Describe(SchemaDescription description)
    {
        var known = format switch
        {
            "email" => "email",
            "url" => "uri",
            "uuid" => "uuid",
            "hostname" => "hostname",
            _ => null,
        };

        if (known is not null)
        {
            description.Format = known;
            return;
        }

        description.Pattern = pattern.ToString();
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        if (pattern.IsMatch(value))
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = format,
            },
            Message);
    }
}

internal sealed class SubstringCheck(
    string operand,
    StringComparison comparison,
    SubstringCheck.Kind kind) : Check<string>
{
    internal enum Kind
    {
        StartsWith,
        EndsWith,
        Contains,
    }

    internal override void Run(ref ParseContext context, ref string value)
    {
        var satisfied = kind switch
        {
            Kind.StartsWith => value.StartsWith(operand, comparison),
            Kind.EndsWith => value.EndsWith(operand, comparison),
            _ => value.Contains(operand, comparison),
        };

        if (satisfied)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = kind switch
                {
                    Kind.StartsWith => "starts_with",
                    Kind.EndsWith => "ends_with",
                    _ => "contains",
                },
                Expected = operand,
            },
            Message);
    }
}

// A date, or a date and time, written the way ISO 8601 writes one.
// This is the one format rule that is not a regular expression, and deliberately so. A pattern can
// only describe the shape, and the shape is the easy half: "2026-02-31" is shaped like a date and
// is not one. TryParseExact knows the calendar, costs nothing at run time, and removes a pattern
// from the set that has to be argued about instead of adding one to it.
internal sealed class Iso8601Check(Iso8601Check.Kind kind) : Check<string>
{
    internal enum Kind
    {
        DateTime,
        Date,
    }

    // Seconds and fractional seconds are optional, and so is the offset: a value with no offset is
    // what .NET writes for a DateTime whose Kind is Unspecified, and rejecting it would reject the
    // most common thing the platform itself produces. The separator is 'T' and nothing else — a
    // space instead is not a typo, it is a different serialization.
    private static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mmK",
    ];

    internal override void Describe(SchemaDescription description) =>
        description.Format = kind == Kind.Date ? "date" : "date-time";

    internal override void Run(ref ParseContext context, ref string value)
    {
        var satisfied = kind switch
        {
            Kind.Date => DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
            _ => DateTimeOffset.TryParseExact(
                value,
                DateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _),
        };

        if (satisfied)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidFormat,
                Origin = ValidationOrigin.Text,
                Format = kind == Kind.Date ? "iso8601_date" : "iso8601",
            },
            Message);
    }
}

internal sealed class RefineCheck<T>(Func<T, bool> predicate, string? message) : Check<T>
{
    internal override void Run(ref ParseContext context, ref T value)
    {
        if (predicate(value))
        {
            return;
        }

        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, message ?? Message);
    }
}
