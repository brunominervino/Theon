using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

// The origin is a parameter rather than a constant because a TimeSpan uses these same two rules and
// is not a point in time. "Must be later than 00:05:00" is wrong about a duration; "must be greater
// than 00:05:00" is right, and that is the sentence the Number origin already asks providers for.
internal sealed class NotBeforeCheck<T>(
    T bound,
    bool inclusive,
    ValidationOrigin origin = ValidationOrigin.DateTime) : Check<T>
    where T : IComparable<T>
{
    // Recorded even though the dialect has no keyword for a date range, because the description is
    // the library's own model and a later consumer may have somewhere to put it. The writer decides
    // what survives into a document.
    internal override void Describe(SchemaDescription description)
    {
        description.Minimum = bound;
        description.ExclusiveMinimum = !inclusive;
    }

    internal override void Run(ref ParseContext context, ref T value)
    {
        var comparison = value.CompareTo(bound);
        if (inclusive ? comparison >= 0 : comparison > 0)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = origin,
                Minimum = bound,
                Inclusive = inclusive,
            },
            Message);
    }
}

internal sealed class NotAfterCheck<T>(
    T bound,
    bool inclusive,
    ValidationOrigin origin = ValidationOrigin.DateTime) : Check<T>
    where T : IComparable<T>
{
    internal override void Describe(SchemaDescription description)
    {
        description.Maximum = bound;
        description.ExclusiveMaximum = !inclusive;
    }

    internal override void Run(ref ParseContext context, ref T value)
    {
        var comparison = value.CompareTo(bound);
        if (inclusive ? comparison <= 0 : comparison < 0)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = origin,
                Maximum = bound,
                Inclusive = inclusive,
            },
            Message);
    }
}

// Requires a DateTime to carry a specific DateTimeKind.
// A DateTime whose Kind is
// Unspecified compares against a UTC instant as though it were already
// UTC, and against a local one as though it were local. Neither is checked, neither errors, and
// the two disagree by the machine's offset. Requiring a kind turns that silent class of bug into
// a validation failure at the boundary.
internal sealed class RequireKindCheck(DateTimeKind kind) : Check<DateTime>
{
    internal override bool AbortsOnFailure => true;

    internal override void Run(ref ParseContext context, ref DateTime value)
    {
        if (value.Kind == kind)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = $"a {kind} DateTime",
                Received = value.Kind.ToString(),
            },
            Message);
    }
}
