using System.Numerics;
using Theon.Errors;

namespace Theon.Checks;

internal sealed class GreaterThanCheck<T>(T bound, bool inclusive) : Check<T>
    where T : INumber<T>
{
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
