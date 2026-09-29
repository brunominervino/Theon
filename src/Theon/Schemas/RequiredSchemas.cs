using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Rejects <see langword="null"/> for a value type, and otherwise defers to an inner schema.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
/// <remarks>
/// The counterpart to <c>AllowNull</c>. It exists because a property declared <c>DateTime?</c>
/// needs a <c>Schema&lt;DateTime?&gt;</c>, and the only ones available were those that accept
/// <see langword="null"/>. This one has the nullable shape and refuses the null.
/// </remarks>
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

/// <summary>
/// Rejects <see langword="null"/> for a reference type, and otherwise defers to an inner schema.
/// </summary>
/// <typeparam name="T">The reference type.</typeparam>
/// <remarks>
/// Most reference schemas already reject <see langword="null"/>, so this changes no behaviour for
/// them. What it changes is the declared type: a property annotated <c>string?</c> can be given a
/// schema that is honestly typed <c>Schema&lt;string?&gt;</c> and still refuses the null, instead
/// of the call site papering over the annotation.
/// </remarks>
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
