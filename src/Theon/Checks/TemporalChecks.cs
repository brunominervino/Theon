using Theon.Errors;

namespace Theon.Checks;

internal sealed class NotBeforeCheck<T>(T bound, bool inclusive) : Check<T>
    where T : IComparable<T>
{
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
                Origin = ValidationOrigin.DateTime,
                Minimum = bound,
                Inclusive = inclusive,
            },
            Message);
    }
}

internal sealed class NotAfterCheck<T>(T bound, bool inclusive) : Check<T>
    where T : IComparable<T>
{
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
                Origin = ValidationOrigin.DateTime,
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
