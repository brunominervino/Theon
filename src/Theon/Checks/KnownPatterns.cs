using System.Text.RegularExpressions;

namespace Theon.Checks;

/// <summary>
/// The built-in format patterns.
/// </summary>
/// <remarks>
/// <para>
/// Every pattern here is compiled by the regular-expression source generator, so it costs no
/// reflection, survives trimming, and works under Native AOT.
/// </para>
/// <para>
/// They are also declared <see cref="RegexOptions.NonBacktracking"/>. These patterns are pointed at
/// text supplied by strangers, and a backtracking engine turns a hostile string into a denial of
/// service. Non-backtracking matching is linear in the length of the input by construction, which
/// removes that whole class of problem rather than trying to outrun it.
/// </para>
/// <para>
/// Matching the specification exactly is explicitly not the goal. An address that is legal by
/// RFC 5322 but that no mail provider would ever issue is still a typo in a sign-up form, and
/// widening the pattern to admit it costs every caller. Use <c>Matches</c> with your own pattern
/// where a particular exotic form genuinely has to be accepted.
/// </para>
/// </remarks>
internal static partial class KnownPatterns
{
    /// <summary>A pragmatic e-mail address: a dot-separated local part, an at sign, a dotted domain.</summary>
    [GeneratedRegex(
        @"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,}$",
        RegexOptions.NonBacktracking)]
    internal static partial Regex Email();

    /// <summary>A string containing no lowercase ASCII letter.</summary>
    [GeneratedRegex("^[^a-z]*$", RegexOptions.NonBacktracking)]
    internal static partial Regex Uppercase();

    /// <summary>A string containing no uppercase ASCII letter.</summary>
    [GeneratedRegex("^[^A-Z]*$", RegexOptions.NonBacktracking)]
    internal static partial Regex Lowercase();
}
