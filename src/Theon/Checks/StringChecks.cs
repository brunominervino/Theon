using System.Globalization;
using System.Text.RegularExpressions;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

internal sealed class MinLengthCheck(int minimum) : Check<string>
{
    internal override void Describe(SchemaDescriptionBuilder description) =>
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
    internal override void Describe(SchemaDescriptionBuilder description) =>
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
    internal override void Describe(SchemaDescriptionBuilder description)
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
    internal override void Describe(SchemaDescriptionBuilder description)
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
        Time,
        Duration,
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

    // No offset among these, because a TimeOnly has nowhere to put one. A time of day with an offset
    // is a different idea, and the value that carries it is a DateTimeOffset.
    private static readonly string[] TimeFormats =
    [
        "HH':'mm':'ss",
        "HH':'mm':'ss'.'FFFFFFF",
        "HH':'mm",
    ];

    internal override void Describe(SchemaDescriptionBuilder description)
    {
        switch (kind)
        {
            case Kind.Date:
                description.Format = "date";
                break;

            case Kind.Duration:
                description.Format = "duration";
                break;

            case Kind.Time:
                // The dialect's "time" is RFC 3339 full-time, which requires an offset, and this rule
                // accepts a time of day without one. Claiming the format would make a document stricter
                // than the schema, so a client generated from it would refuse a value the server takes.
                // Saying nothing is the honest answer, and the unrepresentable report names it.
                description.CannotRepresent("Iso8601Time");
                break;

            default:
                description.Format = "date-time";
                break;
        }
    }

    // An ISO 8601 duration: P, then years, months and days in that order, then optionally T and hours,
    // minutes and seconds in that order. A scan rather than a pattern or a framework call: a pattern
    // cannot enforce the ordering without alternation that grows fast, and XmlConvert.ToTimeSpan throws
    // instead of answering, which is the wrong shape for a rule that reports.
    //
    // Two deliberate departures from the specification. A leading minus sign is refused, because a
    // negative duration in a configuration value is a mistake rather than an intention. A fraction is
    // allowed on any component, not only the last, because producers vary and PT0.5H is not a typo.
    private static bool IsDuration(ReadOnlySpan<char> value)
    {
        if (value.Length < 3 || value[0] != 'P')
        {
            return false;
        }

        var i = 1;
        var sawComponent = false;
        var inTime = false;
        var next = 0;

        ReadOnlySpan<char> dateUnits = "YMD";
        ReadOnlySpan<char> timeUnits = "HMS";

        while (i < value.Length)
        {
            if (value[i] == 'T')
            {
                if (inTime)
                {
                    return false;
                }

                inTime = true;
                next = 0;
                i++;

                // A T has to introduce something.
                if (i == value.Length)
                {
                    return false;
                }

                continue;
            }

            var start = i;
            while (i < value.Length && (uint)(value[i] - '0') <= 9)
            {
                i++;
            }

            if (i == start)
            {
                return false;
            }

            if (i < value.Length && (value[i] == '.' || value[i] == ','))
            {
                i++;
                var fraction = i;
                while (i < value.Length && (uint)(value[i] - '0') <= 9)
                {
                    i++;
                }

                if (i == fraction)
                {
                    return false;
                }
            }

            // A number with no unit after it.
            if (i == value.Length)
            {
                return false;
            }

            var unit = value[i];
            i++;

            // The week form stands alone: a count of weeks cannot be combined with anything else.
            if (unit == 'W')
            {
                return !inTime && !sawComponent && i == value.Length;
            }

            // Searching the remaining units rather than all of them enforces the order and rejects a
            // repeat in the same step.
            var expected = inTime ? timeUnits : dateUnits;
            var at = expected[next..].IndexOf(unit);
            if (at < 0)
            {
                return false;
            }

            next += at + 1;
            sawComponent = true;
        }

        return sawComponent;
    }

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
            Kind.Time => TimeOnly.TryParseExact(
                value,
                TimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _),
            Kind.Duration => IsDuration(value),
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
                Format = kind switch
                {
                    Kind.Date => "iso8601_date",
                    Kind.Time => "iso8601_time",
                    Kind.Duration => "iso8601_duration",
                    _ => "iso8601",
                },
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
