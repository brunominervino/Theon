using System.Text.RegularExpressions;
using Theon.Checks;
using Theon.Errors;
using Theon.Metadata;

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

    /// <summary>Requires a well-formed web address.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// Accepts <c>http</c> and <c>https</c> only, with a host that is either a dotted name or
    /// <c>localhost</c>, and optionally a port, path, query and fragment.
    /// </para>
    /// <para>
    /// Three things it refuses that a specification would allow, each on purpose. Other schemes,
    /// because a field labelled "website" that accepts <c>javascript:</c> is a vulnerability rather
    /// than a lenient validator. Credentials before the host, because nobody means to type them
    /// into a form. And a single-label host such as <c>https://intranet</c>, because in a web
    /// address that is nearly always a half-finished domain name. Use
    /// <see cref="Matches(Regex, string, string)"/> where one of those genuinely has to pass.
    /// </para>
    /// </remarks>
    public StringSchema Url(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Url(), "url") { Message = message });

    /// <summary>Requires a UUID in the canonical hyphenated form, in either case.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// For an identifier that travels as text and is never converted — a route parameter, a header,
    /// a column in a file. Where the value is already a <see cref="System.Guid"/>, use
    /// <see cref="Theo.Guid"/> instead, which has nothing to parse.
    /// </para>
    /// <para>
    /// The version and variant digits are not constrained, so a UUIDv7 and the all-zero UUID both
    /// pass. A mistyped identifier comes out the wrong length or with a character that is not a
    /// hexadecimal digit; it does not come out as a well-formed UUID of an unexpected version.
    /// </para>
    /// </remarks>
    public StringSchema Uuid(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Uuid(), "uuid") { Message = message });

    /// <summary>Requires base64 with the standard alphabet and correct padding.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// The empty string passes, because it is the encoding of the empty byte array and round-trips
    /// as one. Chain <see cref="NotEmpty"/> where a value is also required.
    /// </remarks>
    public StringSchema Base64(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Base64(), "base64") { Message = message });

    /// <summary>Requires base64 with the URL-safe alphabet and no padding.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// The alphabet of RFC 4648 section 5, which uses <c>-</c> and <c>_</c> in place of <c>+</c>
    /// and <c>/</c>: the form a JSON web token, a URL segment or a filename carries. Padding is
    /// refused rather than tolerated, because none of those carry it, so a padded value here was
    /// produced by the wrong encoder.
    /// </remarks>
    public StringSchema Base64Url(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Base64Url(), "base64url") { Message = message });

    /// <summary>Requires one or more hexadecimal digits, in either case.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// An even number of digits is not required, because "a hexadecimal number" is as common a
    /// reading as "hexadecimal bytes". Where the value is a fixed-width digest or key, say so:
    /// <c>Hex().Length(64)</c> is a SHA-256.
    /// </remarks>
    public StringSchema Hex(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Hex(), "hex") { Message = message });

    /// <summary>Requires a telephone number in E.164 form.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// A plus sign, a country code, and up to fifteen digits in total:
    /// <c>+5511987654321</c>. Spaces, hyphens and parentheses are refused. E.164 is the form a
    /// number is stored and dialled in; how it is displayed is a different problem, and accepting a
    /// displayed number here would mean storing one the next system cannot dial. Normalize with
    /// <see cref="Normalize"/> first if your form lets people type the pretty version.
    /// </remarks>
    public StringSchema E164(string? message = null) =>
        With(new PatternCheck(KnownPatterns.E164(), "e164") { Message = message });

    /// <summary>Requires an IPv4 address in dotted-decimal form.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// A leading zero is refused: <c>010.1.1.1</c> is read as octal by some resolvers and as decimal by
    /// others, and an address that means two different things is worse than no address at all.
    /// </remarks>
    public StringSchema Ipv4(string? message = null) =>
        With(new Ipv4Check { Message = message });

    /// <summary>Requires an IPv6 address.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Accepts the compressed <c>::</c> form and an embedded IPv4 tail such as
    /// <c>::ffff:192.168.0.1</c>. A zone identifier — the <c>%eth0</c> on a link-local address — is
    /// refused, because it names an interface on one machine rather than a host on a network; use
    /// <see cref="Matches(Regex, string, string)"/> where one has to pass.
    /// </remarks>
    public StringSchema Ipv6(string? message = null) =>
        With(new Ipv6Check { Message = message });

    /// <summary>Requires an address and prefix length in CIDR notation.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Either family: <c>10.0.0.0/8</c> or <c>2001:db8::/32</c>. The prefix length is checked against
    /// the bound for the family the address belongs to, so <c>10.0.0.0/64</c> is refused.
    /// </remarks>
    public StringSchema Cidr(string? message = null) =>
        With(new CidrCheck { Message = message });

    /// <summary>Requires a host name.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Labels of letters, digits and interior hyphens, separated by dots. A single label passes, because
    /// <c>localhost</c> is what a configuration field holds — which is the difference between this and
    /// the host inside <see cref="Url"/>, where a single label is nearly always a half-typed domain. A
    /// trailing dot is refused. The 253-character total is not part of the rule; compose
    /// <see cref="MaxLength"/> where it matters.
    /// </remarks>
    public StringSchema Hostname(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Hostname(), "hostname") { Message = message });

    /// <summary>Requires a JSON web token.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Three non-empty base64url segments separated by dots. This checks the shape and nothing else —
    /// it does not verify the signature, and no validation rule can. Requiring the third segment to be
    /// non-empty refuses the unsecured token whose algorithm is <c>none</c>, which is legal and is a
    /// way in.
    /// </remarks>
    public StringSchema Jwt(string? message = null) =>
        With(new PatternCheck(KnownPatterns.Jwt(), "jwt") { Message = message });

    /// <summary>Requires a card number that satisfies the Luhn checksum.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Twelve to nineteen digits, and the checksum has to come out. The checksum is the point: a
    /// pattern can only say how many digits there are, while Luhn catches the single mistyped digit and
    /// the two transposed ones, which is what people actually do.
    /// <para>
    /// Digits only. Spaces and hyphens are refused for the same reason <see cref="E164"/> refuses them:
    /// this is the form the value is sent in, and presentation is a different problem. Use
    /// <see cref="Normalize"/> where the form lets people type the pretty version.
    /// </para>
    /// </remarks>
    public StringSchema CreditCard(string? message = null) =>
        With(new CreditCardCheck { Message = message });

    /// <summary>Requires an IBAN that satisfies the mod-97 checksum of ISO 13616.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Uppercase and unspaced, which is the interchange form. Chain
    /// <see cref="ToUpperInvariant"/> where callers may send it lowercase. What this cannot check is
    /// whether the country's own account-number layout is respected, only that the checksum agrees.
    /// </remarks>
    public StringSchema Iban(string? message = null) =>
        With(new IbanCheck { Message = message });

    /// <summary>Requires a date and time written the way ISO 8601 writes one.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// <c>2026-09-30T14:30:00Z</c>, and also the forms with fractional seconds, with a numeric
    /// offset, with no offset at all, and with the seconds left off. The separator is <c>T</c>; a
    /// space in its place is a different serialization, not a typo, and is refused.
    /// </para>
    /// <para>
    /// The calendar is checked, not just the shape, so <c>2026-02-31</c> fails. That is the reason
    /// this rule is not a regular expression: a pattern cannot tell February from the number 31.
    /// </para>
    /// <para>
    /// This is for a value that really is a <see cref="string"/> — a query parameter, a cell in a
    /// file, a field a legacy service sends as text. Where the value arrives already typed, use
    /// <see cref="Theo.DateTimeOffset"/>, which has nothing left to parse.
    /// </para>
    /// </remarks>
    public StringSchema Iso8601(string? message = null) =>
        With(new Iso8601Check(Iso8601Check.Kind.DateTime) { Message = message });

    /// <summary>Requires a calendar date written the way ISO 8601 writes one.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <c>2026-09-30</c>, and nothing with a time attached. The calendar is checked, so
    /// <c>2026-02-31</c> and <c>2027-02-29</c> both fail.
    /// </remarks>
    public StringSchema Iso8601Date(string? message = null) =>
        With(new Iso8601Check(Iso8601Check.Kind.Date) { Message = message });

    /// <summary>Requires a time of day written the way ISO 8601 writes one.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// <c>14:30:00</c>, and also with fractional seconds or with the seconds left off. The clock is
    /// checked, not just the shape, so <c>24:00:00</c> and <c>14:60:00</c> both fail.
    /// </para>
    /// <para>
    /// No offset. A time of day with one is a different idea, and the value that carries it is a
    /// <see cref="System.DateTimeOffset"/>; a <see cref="System.TimeOnly"/> has nowhere to put it. That
    /// is also why a generated document says nothing about this rule: the dialect's <c>time</c> format
    /// is RFC 3339, which requires the offset, and claiming it would make the document stricter than
    /// the schema.
    /// </para>
    /// </remarks>
    public StringSchema Iso8601Time(string? message = null) =>
        With(new Iso8601Check(Iso8601Check.Kind.Time) { Message = message });

    /// <summary>Requires a duration written the way ISO 8601 writes one.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// <para>
    /// <c>P1Y2M3DT4H5M6S</c>, <c>PT5S</c>, <c>P3W</c>, and the forms in between. The order is enforced
    /// and a repeated component is refused, so <c>PT1D</c> and <c>P1M2Y</c> both fail where a looser
    /// rule would let them through.
    /// </para>
    /// <para>
    /// This is not the form .NET writes for a <see cref="System.TimeSpan"/>, which is
    /// <c>00:00:05</c> — use <see cref="Theo.TimeSpan"/> for that. This is for the duration a JSON
    /// document or an XML payload carries, and the months and years it can express have no
    /// <see cref="System.TimeSpan"/> to hold them.
    /// </para>
    /// <para>
    /// Two deliberate departures. A leading minus is refused, because a negative duration in a
    /// configuration value is a mistake rather than an intention. A fraction is allowed on any
    /// component rather than only the last, because producers vary and <c>PT0.5H</c> is not a typo.
    /// </para>
    /// </remarks>
    public StringSchema Iso8601Duration(string? message = null) =>
        With(new Iso8601Check(Iso8601Check.Kind.Duration) { Message = message });

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
    public override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.String, _checks);

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
