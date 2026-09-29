using Theon.Errors;

namespace Theon.Checks;

internal sealed class MinCountCheck<T>(int minimum) : Check<IReadOnlyList<T>>
{
    internal override void Run(ref ParseContext context, ref IReadOnlyList<T> value)
    {
        if (value.Count >= minimum)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Collection,
                Minimum = minimum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class MaxCountCheck<T>(int maximum) : Check<IReadOnlyList<T>>
{
    internal override void Run(ref ParseContext context, ref IReadOnlyList<T> value)
    {
        if (value.Count <= maximum)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Collection,
                Maximum = maximum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class ExactCountCheck<T>(int count) : Check<IReadOnlyList<T>>
{
    internal override void Run(ref ParseContext context, ref IReadOnlyList<T> value)
    {
        if (value.Count == count)
        {
            return;
        }

        context.AddError(
            value.Count < count
                ? new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Collection,
                    Minimum = count,
                    Inclusive = true,
                }
                : new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Collection,
                    Maximum = count,
                    Inclusive = true,
                },
            Message);
    }
}
