using System.Text.RegularExpressions;
using Theon.Checks;
using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Validates and normalizes a <see cref="string"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every method returns a new schema, so a schema can be built once and shared. Rules run in the
/// order they were written, which matters whenever one of them rewrites the value:
/// <c>Trim().MinLength(3)</c> measures the trimmed string, <c>MinLength(3).Trim()</c> measures the
/// original.
/// </para>
/// <para>
/// A <see langword="null"/> input fails with <see cref="ValidationErrorCode.InvalidType"/>. Call
/// <see cref="AllowNull"/> to accept it.
/// </para>
/// </remarks>
public sealed class StringSchema : Schema<string>
{
    private readonly Check<string>[] _checks;

    internal StringSchema()
        : this([])
    {
    }

    private StringSchema(Check<string>[] checks) => _checks = checks;

    private StringSchema With(Check<string> check) => new([.. _checks, check]);

    /// <summary>Requires at least <paramref name="minimum"/> characters.</summary>
    /// <param name="minimum">The smallest allowed length, in Unicode code points.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema MinLength(int minimum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        return With(new MinLengthCheck(minimum) { Message = message });
    }

    /// <summary>Requires at most <paramref name="maximum"/> characters.</summary>
    /// <param name="maximum">The largest allowed length, in Unicode code points.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema MaxLength(int maximum, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        return With(new MaxLengthCheck(maximum) { Message = message });
    }

    /// <summary>Requires exactly <paramref name="length"/> characters.</summary>
    /// <param name="length">The required length, in Unicode code points.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema Length(int length, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        return With(new ExactLengthCheck(length) { Message = message });
    }

    /// <summary>Requires at least one character.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema NotEmpty(string? message = null) => MinLength(1, message);

    /// <summary>Removes leading and trailing whitespace before the rules that follow.</summary>
    public StringSchema Trim() => With(new OverwriteCheck(static value => value.Trim()));

    /// <summary>Lowercases the value using the invariant culture, before the rules that follow.</summary>
    /// <remarks>
    /// The invariant culture is deliberate. Culture-sensitive casing makes the same schema behave
    /// differently depending on the ambient culture, and in Turkish it maps <c>I</c> to a dotless
    /// <c>i</c>, which silently corrupts identifiers and e-mail addresses.
    /// </remarks>
    public StringSchema ToLowerInvariant() =>
        With(new OverwriteCheck(static value => value.ToLowerInvariant()));

    /// <summary>Uppercases the value using the invariant culture, before the rules that follow.</summary>
    /// <inheritdoc cref="ToLowerInvariant" path="/remarks"/>
    public StringSchema ToUpperInvariant() =>
        With(new OverwriteCheck(static value => value.ToUpperInvariant()));

    /// <summary>Applies an arbitrary normalization before the rules that follow.</summary>
    /// <param name="normalize">The normalization to apply. Must not return <see langword="null"/>.</param>
    public StringSchema Normalize(Func<string, string> normalize)
    {
        ArgumentNullException.ThrowIfNull(normalize);
        return With(new OverwriteCheck(normalize));
    }

    /// <summary>Requires a well-formed e-mail address.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// The pattern is pragmatic rather than exhaustively RFC 5322 compliant: it accepts the
    /// addresses a mail provider will actually issue and rejects the rest. Use
    /// <see cref="Matches(Regex, string, string)"/> if you need a form this rejects.
    /// </remarks>
    public StringSchema Email(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Email(), "email") { Message = message });

    /// <summary>Requires the value to match <paramref name="pattern"/>.</summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="format">A short name for this format, used by message providers.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// The pattern is matched as given; it is not anchored for you. Prefer a pattern produced by
    /// <see cref="GeneratedRegexAttribute"/>, and give it a timeout or
    /// <see cref="RegexOptions.NonBacktracking"/> if it will see text from strangers.
    /// </remarks>
    public StringSchema Matches(Regex pattern, string format = "regex", string? message = null)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrEmpty(format);
        return With(new PatternCheck(pattern, format) { Message = message });
    }

    /// <summary>Requires the value to contain no uppercase ASCII letter.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema Lowercase(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Lowercase(), "lowercase") { Message = message });

    /// <summary>Requires the value to contain no lowercase ASCII letter.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema Uppercase(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Uppercase(), "uppercase") { Message = message });

    /// <summary>Requires the value to start with <paramref name="prefix"/>.</summary>
    /// <param name="prefix">The required prefix.</param>
    /// <param name="comparison">How to compare. Ordinal by default.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema StartsWith(
        string prefix,
        StringComparison comparison = StringComparison.Ordinal,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return With(new SubstringCheck(prefix, comparison, SubstringCheck.Kind.StartsWith) { Message = message });
    }

    /// <summary>Requires the value to end with <paramref name="suffix"/>.</summary>
    /// <param name="suffix">The required suffix.</param>
    /// <param name="comparison">How to compare. Ordinal by default.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema EndsWith(
        string suffix,
        StringComparison comparison = StringComparison.Ordinal,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(suffix);
        return With(new SubstringCheck(suffix, comparison, SubstringCheck.Kind.EndsWith) { Message = message });
    }

    /// <summary>Requires the value to contain <paramref name="substring"/>.</summary>
    /// <param name="substring">The required substring.</param>
    /// <param name="comparison">How to compare. Ordinal by default.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public StringSchema Contains(
        string substring,
        StringComparison comparison = StringComparison.Ordinal,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(substring);
        return With(new SubstringCheck(substring, comparison, SubstringCheck.Kind.Contains) { Message = message });
    }

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public StringSchema Refine(Func<string, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<string>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<string?> AllowNull() => new NullableReferenceSchema<string>(this);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, string input, out string output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "string",
                Received = "null",
            });

            output = string.Empty;
            return false;
        }

        output = input;

        var aborted = false;
        foreach (var check in _checks)
        {
            if (aborted || context.ShouldStop)
            {
                break;
            }

            var before = context.ErrorCount;
            check.Run(ref context, ref output);

            if (context.ErrorCount > before && check.AbortsOnFailure)
            {
                aborted = true;
            }
        }

        return context.ErrorCount == errorsBefore;
    }
}
