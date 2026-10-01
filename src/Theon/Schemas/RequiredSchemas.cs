using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

// Rejects null for a value type, and otherwise defers to an inner schema.
// The underlying value type.
// The counterpart to AllowNull. It exists because a property declared DateTime?
// needs a Schema<DateTime?>, and the only ones available were those that accept
// null. This one has the nullable shape and refuses the null.
internal sealed class RequiredValueSchema<T>(Schema<T> inner, string? message) : Schema<T?>
    where T : struct
{
    // The nullable shape refuses the null, so nothing about the description changes: a document says
    // "this may not be null" by leaving null out of the type and by naming the property as required.
    //
    // The same on both sides, which is worth saying because it looks as though it should not be. The
    // declared output type is T?, so the reflex is that the output may be null -- but the only way a
    // null leaves here is a parse that failed, and a description describes the values a parse succeeds
    // with. Fixed by test in DescriptionDirectionTests.
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) => context.Describe(inner);

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

    public override async ValueTask<ParseOutcome<T?>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            context.AddError(
                new Errors.ValidationErrorInfo
                {
                    Code = Errors.ValidationErrorCode.InvalidType,
                    Expected = typeof(T).Name,
                    Received = "null",
                },
                message);

            return new ParseOutcome<T?>(false, null);
        }

        var outcome = await inner.TryParseAsync(context, input.Value).ConfigureAwait(false);
        return new ParseOutcome<T?>(outcome.Succeeded, outcome.Value);
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
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) => context.Describe(inner);

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

    public override async ValueTask<ParseOutcome<T?>> TryParseAsync(AsyncParseContext context, T? input)
    {
        if (input is null)
        {
            context.AddError(
                new Errors.ValidationErrorInfo
                {
                    Code = Errors.ValidationErrorCode.InvalidType,
                    Expected = typeof(T).Name,
                    Received = "null",
                },
                message);

            return new ParseOutcome<T?>(false, null);
        }

        var outcome = await inner.TryParseAsync(context, input).ConfigureAwait(false);
        return new ParseOutcome<T?>(outcome.Succeeded, outcome.Value);
    }
}
