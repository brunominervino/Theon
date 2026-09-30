using System.Numerics;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

internal sealed class GreaterThanCheck<T>(T bound, bool inclusive) : Check<T>
    where T : INumber<T>
{
    internal override void Describe(SchemaDescription description)
    {
        description.Minimum = bound;
        description.ExclusiveMinimum = !inclusive;
    }

    internal override void Run(ref ParseContext context, ref T value)
    {
        if (inclusive ? value >= bound : value > bound)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Number,
                Minimum = bound,
                Inclusive = inclusive,
            },
            Message);
    }
}

internal sealed class LessThanCheck<T>(T bound, bool inclusive) : Check<T>
    where T : INumber<T>
{
    internal override void Describe(SchemaDescription description)
    {
        description.Maximum = bound;
        description.ExclusiveMaximum = !inclusive;
    }

    internal override void Run(ref ParseContext context, ref T value)
    {
        if (inclusive ? value <= bound : value < bound)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Number,
                Maximum = bound,
                Inclusive = inclusive,
            },
            Message);
    }
}

internal sealed class MultipleOfCheck<T>(T divisor) : Check<T>
    where T : INumber<T>
{
    internal override void Describe(SchemaDescription description) =>
        description.MultipleOf = divisor;

    internal override void Run(ref ParseContext context, ref T value)
    {
        if (value % divisor == T.Zero)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.NotMultipleOf,
                Origin = ValidationOrigin.Number,
                Divisor = divisor,
            },
            Message);
    }
}
