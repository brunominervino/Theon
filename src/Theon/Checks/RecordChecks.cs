using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

internal sealed class RecordCountCheck<TKey, TValue>(int? minimum, int? maximum)
    : Check<IReadOnlyDictionary<TKey, TValue>>
    where TKey : notnull
{
    internal override void Describe(SchemaDescriptionBuilder description)
    {
        description.MinItems = minimum;
        description.MaxItems = maximum;
    }

    internal override void Run(ref ParseContext context, ref IReadOnlyDictionary<TKey, TValue> value)
    {
        var count = value.Count;

        if (minimum is { } min && count < min)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Collection,
                    Minimum = min,
                    Inclusive = true,
                },
                Message);
            return;
        }

        if (maximum is { } max && count > max)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Collection,
                    Maximum = max,
                    Inclusive = true,
                },
                Message);
        }
    }
}
