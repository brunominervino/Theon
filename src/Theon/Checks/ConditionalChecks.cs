using Theon.Errors;

namespace Theon.Checks;

/// <summary>
/// A refinement that is only asked when a condition holds.
/// </summary>
/// <typeparam name="T">The object type being refined.</typeparam>
/// <remarks>
/// The same outcome can be written as a single predicate, <c>!condition || requirement</c>, but
/// that is a material implication spelled as a disjunction: correct, and read wrongly by almost
/// everyone almost every time. Keeping the two halves apart lets the call site say what it means.
/// </remarks>
internal sealed class ConditionalRefineCheck<T>(
    Func<T, bool> condition,
    Func<T, bool> requirement,
    string? pathName,
    string message) : Check<T>
{
    internal override void Run(ref ParseContext context, ref T value)
    {
        if (!condition(value) || requirement(value))
        {
            return;
        }

        if (pathName is not null)
        {
            context.PushProperty(pathName);
        }

        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, message);

        if (pathName is not null)
        {
            context.Pop();
        }
    }
}

/// <summary>
/// A whole schema that is only applied when a condition holds.
/// </summary>
/// <typeparam name="T">The object type being validated.</typeparam>
/// <remarks>
/// This is the shape a real conditional usually has: a status reaching some value unlocks not one
/// requirement but several, and they want to be written together rather than as a list of
/// individually guarded predicates that each restate the same condition.
/// </remarks>
internal sealed class ConditionalSchemaCheck<T>(Func<T, bool> condition, Schema<T, T> inner) : Check<T>
{
    internal override void Run(ref ParseContext context, ref T value)
    {
        if (!condition(value))
        {
            return;
        }

        inner.TryParse(ref context, value, out _);
    }
}
