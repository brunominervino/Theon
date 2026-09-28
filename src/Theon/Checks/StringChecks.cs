using System.Text.RegularExpressions;
using Theon.Errors;

namespace Theon.Checks;

internal sealed class MinLengthCheck(int minimum) : Check<string>
{
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

/// <summary>A check that rewrites the value instead of rejecting it.</summary>
internal sealed class OverwriteCheck(Func<string, string> transform) : Check<string>
{
    internal override void Run(ref ParseContext context, ref string value) => value = transform(value);
}

internal sealed class PatternCheck(Regex pattern, string format) : Check<string>
{
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
