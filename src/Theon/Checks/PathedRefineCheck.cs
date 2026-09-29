using Theon.Errors;

namespace Theon.Checks;

// A refinement that reports its failure against a named property rather than the object itself.
// The object type being refined.
// A rule like "the two passwords must match" is about the object, but the message belongs on the
// field the user should go back and fix. This check lets the rule live at the object level while
// the error lands where the form can show it.
internal sealed class PathedRefineCheck<T>(Func<T, bool> predicate, string? pathName, string message) : Check<T>
{
    internal override void Run(ref ParseContext context, ref T value)
    {
        if (predicate(value))
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
