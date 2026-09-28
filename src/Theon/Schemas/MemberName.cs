namespace Theon.Schemas;

/// <summary>
/// Recovers a property name from the source text of an accessor lambda.
/// </summary>
/// <remarks>
/// <para>
/// The compiler hands us the literal text a caller wrote, via
/// <see cref="System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"/>. For the shape
/// people actually write, <c>x =&gt; x.Email</c>, the name is whatever follows the last dot. This
/// runs once, while a schema is being built, and never during a parse.
/// </para>
/// <para>
/// Anything it cannot read confidently is rejected rather than guessed at, because a guess would
/// surface later as a wrong path in an error message, which is far harder to trace back than an
/// exception at the line that caused it.
/// </para>
/// </remarks>
internal static class MemberName
{
    internal static string From(string? accessorExpression, string parameterName)
    {
        var expression = accessorExpression?.Trim();

        if (string.IsNullOrEmpty(expression))
        {
            throw new ArgumentException(
                "The property name could not be determined. Use the overload that takes an explicit name.",
                parameterName);
        }

        var lastDot = expression.LastIndexOf('.');
        var candidate = (lastDot >= 0 ? expression[(lastDot + 1)..] : expression).Trim();

        if (!IsIdentifier(candidate))
        {
            throw new ArgumentException(
                $"The property name could not be determined from '{expression}'. " +
                "Use the overload that takes an explicit name.",
                parameterName);
        }

        return candidate;
    }

    private static bool IsIdentifier(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        if (value[0] != '_' && !char.IsLetter(value[0]))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c != '_' && !char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
