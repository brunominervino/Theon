using System.Text.RegularExpressions;

namespace Theon.Checks;

// The built-in format patterns.
// Every pattern here is compiled by the regular-expression source generator, so it costs no
// reflection, survives trimming, and works under Native AOT.
// They are also declared NonBacktracking. These patterns are pointed at
// text supplied by strangers, and a backtracking engine turns a hostile string into a denial of
// service. Non-backtracking matching is linear in the length of the input by construction, which
// removes that whole class of problem rather than trying to outrun it.
// Matching the specification exactly is explicitly not the goal. An address that is legal by
// RFC 5322 but that no mail provider would ever issue is still a typo in a sign-up form, and
// widening the pattern to admit it costs every caller. Use Matches with your own pattern
// where a particular exotic form genuinely has to be accepted.
// Every pattern is anchored with \A and \z, never ^ and $. In .NET, $ also matches immediately
// before a single trailing newline, so ^...$ accepts an address with a line break glued to the end
// of it — which is nobody's address, and which lets a stray line break survive a rule whose whole
// job is to reject one. \z is the true end of the input and admits nothing after it.
internal static partial class KnownPatterns
{
    // A pragmatic e-mail address: a dot-separated local part, an at sign, a dotted domain.
    [GeneratedRegex(
        @"\A[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Email();

    // A string containing no lowercase ASCII letter.
    [GeneratedRegex(@"\A[^a-z]*\z", RegexOptions.NonBacktracking)]
    internal static partial Regex Uppercase();

    // A string containing no uppercase ASCII letter.
    [GeneratedRegex(@"\A[^A-Z]*\z", RegexOptions.NonBacktracking)]
    internal static partial Regex Lowercase();

    // An http or https address with a host, and optionally a port, path, query and fragment.
    // Three deliberate restrictions, none of which the specification asks for:
    // only http and https, because a field labelled "website" that accepts a javascript: URL is a
    // vulnerability rather than a lenient validator; no userinfo, because credentials in a URL a
    // person typed are a mistake worth reporting; and a host that is either a dotted name or
    // exactly "localhost", because a single-label host in a web address is nearly always a
    // half-typed domain, while localhost is what every configuration field holds in development.
    [GeneratedRegex(
        @"\Ahttps?://(?:localhost|[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+)(?::[0-9]{1,5})?(?:[/?#][^\s]*)?\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Url();

    // The canonical hyphenated form of a UUID, in either case.
    // The version and variant nibbles are not constrained. A person mistyping an identifier
    // produces the wrong length or a character that is not a hexadecimal digit; they do not
    // produce a well-formed UUID whose variant bits are unfashionable. Constraining them would
    // also reject the all-zero UUID, which Guid.Empty serializes to and which callers do send.
    [GeneratedRegex(
        @"\A[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Uuid();

    // Base64 with the standard alphabet and correct padding, per RFC 4648 section 4.
    // The empty string matches: it is the encoding of the empty byte array, and it round-trips.
    // Chain NotEmpty where a value is also required.
    [GeneratedRegex(
        @"\A(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Base64();

    // Base64 with the URL and filename safe alphabet, per RFC 4648 section 5, and no padding.
    // Padding is rejected rather than tolerated. The whole reason to reach for this alphabet is to
    // put the value in a URL, a filename or a JSON web token, and none of those carry the equals
    // sign — so a padded value here was encoded with the wrong function.
    [GeneratedRegex(
        @"\A(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-]{2,3})?\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Base64Url();

    // One or more hexadecimal digits, in either case.
    // Unlike base64, the empty string is rejected: no encoder emits it for anything, so it is
    // always an absent value wearing the wrong error. An even length is not required, because
    // "hexadecimal number" is as common a reading as "hexadecimal bytes"; Length(64) says the
    // latter precisely.
    [GeneratedRegex(@"\A[0-9A-Fa-f]+\z", RegexOptions.NonBacktracking)]
    internal static partial Regex Hex();

    // A host name, per RFC 1123: labels of letters, digits and interior hyphens, separated by dots.
    // A single label is accepted, because "localhost" is what a configuration field holds and this is
    // a configuration value rather than a web address a person typed. A trailing dot is not: the
    // absolute form is correct and is never what someone means to enter in a form. The total length
    // limit of 253 characters is not expressible here; compose MaxLength where it matters.
    [GeneratedRegex(
        @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Hostname();

    // A JSON web token: three base64url segments separated by dots.
    // All three have to be non-empty, which rejects the unsecured token whose algorithm is "none".
    // That token is legal and is a way in, and refusing it is the same judgement Url makes about the
    // javascript scheme: the specification is not the thing being served here.
    [GeneratedRegex(
        @"\A[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Jwt();

    // An E.164 telephone number: a plus sign, a country code that does not start with zero, and
    // between two and fifteen digits in total.
    // Nothing else is admitted, in particular none of the spaces, hyphens and parentheses people
    // type. E.164 is the storage and interchange form; presentation is a different problem, and
    // accepting presentation here would mean storing a value the next system cannot dial.
    [GeneratedRegex(@"\A\+[1-9][0-9]{1,14}\z", RegexOptions.NonBacktracking)]
    internal static partial Regex E164();
}
