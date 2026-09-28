namespace Theon.Schemas;

/// <summary>
/// Accepts <see langword="null"/>, and otherwise defers to an inner schema for a reference type.
/// </summary>
/// <typeparam name="T">The non-null type validated by the inner schema.</typeparam>
/// <remarks>
/// C# has no separate notion of "absent" as distinct from "null", so there is one wrapper here
/// where a TypeScript library needs two. See <c>docs/decisions/0004-nullability.md</c>.
/// </remarks>
internal sealed class NullableReferenceSchema<T>(Schema<T> inner) : Schema<T?>
    where T : class
{
    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            output = null;
            return true;
        }

        var succeeded = inner.TryParse(ref context, input, out var parsed);
        output = parsed;
        return succeeded;
    }
}

/// <summary>
/// Accepts <see langword="null"/>, and otherwise defers to an inner schema for a value type.
/// </summary>
/// <typeparam name="T">The underlying value type validated by the inner schema.</typeparam>
internal sealed class NullableValueSchema<T>(Schema<T> inner) : Schema<T?>
    where T : struct
{
    public override bool TryParse(ref ParseContext context, T? input, out T? output)
    {
        if (input is null)
        {
            output = null;
            return true;
        }

        var succeeded = inner.TryParse(ref context, input.Value, out var parsed);
        output = parsed;
        return succeeded;
    }
}
