using Theon.Errors;

namespace Theon.Checks;

// Compares a value against the current instant, read from a imeProvider.
// The clock is injected rather than read from UtcNow so that a rule about
// "the past" can be tested. With an ambient clock, a test for a boundary condition either sleeps,
// or is written against a moving target and fails on a slow machine at midnight.
internal sealed class RelativeToNowCheck<T>(
    TimeProvider timeProvider,
    Func<TimeProvider, T> readNow,
    bool mustBeBefore,
    bool inclusive) : Check<T>
    where T : IComparable<T>
{
    internal override void Run(ref ParseContext context, ref T value)
    {
        var now = readNow(timeProvider);
        var comparison = value.CompareTo(now);

        var satisfied = mustBeBefore
            ? (inclusive ? comparison <= 0 : comparison < 0)
            : (inclusive ? comparison >= 0 : comparison > 0);

        if (satisfied)
        {
            return;
        }

        context.AddError(
            mustBeBefore
                ? new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.DateTime,
                    Maximum = now,
                    Inclusive = inclusive,
                }
                : new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.DateTime,
                    Minimum = now,
                    Inclusive = inclusive,
                },
            Message);
    }
}
