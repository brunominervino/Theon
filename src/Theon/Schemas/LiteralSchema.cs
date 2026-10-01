using System.Globalization;
using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

// Requires one exact value: the "pix" in a payment kind, the 2 in a protocol version.
//
// Equality is EqualityComparer<T>.Default, which for a string is ordinal and for a value type the
// JIT devirtualizes, so the comparison costs nothing and boxes nothing. Case-insensitive matching
// is deliberately not an option here: normalizing first and then comparing exactly is already the
// idiom this library uses for text, and ToLowerInvariant().Literal("pix") says in two rules what
// one rule with a hidden comparer would only imply.
//
// The text for the error is computed once, while the schema is being built, and not at the moment a
// value fails. Schemas are built at start-up and used for the life of the process, so this is the
// one place where formatting is free.
internal sealed class LiteralSchema<T> : Schema<T>
{
    private readonly T _value;
    private readonly string _expected;
    private readonly string? _message;

    internal LiteralSchema(T value, string? message)
    {
        _value = value;
        _message = message;

        // IFormattable with the invariant culture, so a decimal literal is described with a point
        // wherever the process happens to be running. Boxes a value type exactly once, here.
        // The factory rejects a null value, but T is unconstrained and the type cannot say so, so
        // the null-conditional stays rather than a suppression that claims knowledge this file has.
        _expected = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value?.ToString() ?? string.Empty;
    }

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) => new()
    {
        Kind = SchemaKinds.For(typeof(T)),
        ConstantValue = _value,
        HasConstantValue = true,
    };

    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        output = input;

        if (EqualityComparer<T>.Default.Equals(input, _value))
        {
            return true;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.NotEqual,
                Expected = _expected,
            },
            _message);

        return false;
    }
}
