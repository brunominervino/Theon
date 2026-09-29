using Theon.Errors;

namespace Theon.Schemas;

// Rejects null for a value type, and otherwise defers to an inner schema.
// The underlying value type.
// The counterpart to AllowNull. It exists because a property declared DateTime?
// needs a Schema<DateTime?>, and the only ones available were those that accept
// null. This one has the nullable shape and refuses the null.
internal sealed class RequiredValueSchema<T>(Schema<T> inner, string? message) : Schema<T?>
    where T : struct
{
    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.InvalidType,
                    Expected = typeof(T).Name,
                    Received = "null",
                },
                message);

            output = null;
            return false;
        }

        var succeeded = inner.TryParse(ref context, input.Value, out var parsed);
        output = parsed;
        return succeeded;
    }
}

// Rejects null for a reference type, and otherwise defers to an inner schema.
// The reference type.
// Most reference schemas already reject null, so this changes no behaviour for
// them. What it changes is the declared type: a property annotated string? can be given a
// schema that is honestly typed Schema<string?> and still refuses the null, instead
// of the call site papering over the annotation.
internal sealed class RequiredReferenceSchema<T>(Schema<T> inner, string? message) : Schema<T?>
    where T : class
{
    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.InvalidType,
                    Expected = typeof(T).Name,
                    Received = "null",
                },
                message);

            output = null;
            return false;
        }

        var succeeded = inner.TryParse(ref context, input, out var parsed);
        output = parsed;
        return succeeded;
    }
}
