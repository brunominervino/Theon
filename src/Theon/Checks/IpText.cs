namespace Theon.Checks;

// Recognises the textual forms of IP addresses, by scanning rather than by pattern.
//
// Not a regular expression, and not by choice at first. A pattern covering IPv6 with its compressed
// forms and its IPv4 tail needs an automaton of about two thousand two hundred states, and the
// non-backtracking engine refuses to build one over a thousand -- so the linear-time guarantee this
// library relies on is simply not available for that shape. Once IPv6 had to be written by hand, IPv4
// followed it, because one implementation of "is this an address" beats a scanner and a pattern that
// have to agree.
//
// Both scans make a single pass over a span and allocate nothing, which is what the pattern was for.
internal static class IpText
{
    internal static bool IsIpv4(ReadOnlySpan<char> value)
    {
        var octets = 0;
        var i = 0;

        while (true)
        {
            var start = i;
            var number = 0;

            while (i < value.Length && (uint)(value[i] - '0') <= 9)
            {
                number = (number * 10) + (value[i] - '0');
                i++;

                if (i - start > 3)
                {
                    return false;
                }
            }

            var digits = i - start;

            // A leading zero is rejected on purpose. "010.1.1.1" is read as octal by some resolvers
            // and as decimal by others, and an address that means two things is worse than no address.
            if (digits == 0 || number > 255 || (digits > 1 && value[start] == '0'))
            {
                return false;
            }

            octets++;

            if (octets == 4)
            {
                return i == value.Length;
            }

            if (i == value.Length || value[i] != '.')
            {
                return false;
            }

            i++;
        }
    }

    internal static bool IsIpv6(ReadOnlySpan<char> value)
    {
        // The longest form without a zone is an IPv4-mapped address at 45 characters.
        if (value.Length is 0 or > 45)
        {
            return false;
        }

        var groups = 0;
        var compressed = false;
        var i = 0;

        if (value.StartsWith("::"))
        {
            compressed = true;
            i = 2;

            if (i == value.Length)
            {
                return true;
            }
        }
        else if (value[0] == ':')
        {
            return false;
        }

        while (i < value.Length)
        {
            var start = i;
            while (i < value.Length && IsHex(value[i]))
            {
                i++;
            }

            var length = i - start;
            if (length is 0 or > 4)
            {
                return false;
            }

            // A dotted tail is an embedded IPv4 address, and it occupies two groups.
            if (i < value.Length && value[i] == '.')
            {
                if (!IsIpv4(value[start..]))
                {
                    return false;
                }

                groups += 2;
                break;
            }

            groups++;

            if (i == value.Length)
            {
                break;
            }

            if (value[i] != ':')
            {
                return false;
            }

            i++;

            if (i < value.Length && value[i] == ':')
            {
                // A second run of colons would make the number of omitted groups ambiguous.
                if (compressed)
                {
                    return false;
                }

                compressed = true;
                i++;

                if (i == value.Length)
                {
                    break;
                }
            }
        }

        return compressed ? groups < 8 : groups == 8;
    }

    // A prefix length written as decimal digits, within a family's bounds and with no leading zero.
    internal static bool IsPrefixLength(ReadOnlySpan<char> value, int maximum)
    {
        if (value.Length is 0 or > 3 || (value.Length > 1 && value[0] == '0'))
        {
            return false;
        }

        var number = 0;
        foreach (var c in value)
        {
            if ((uint)(c - '0') > 9)
            {
                return false;
            }

            number = (number * 10) + (c - '0');
        }

        return number <= maximum;
    }

    private static bool IsHex(char c) => (uint)(c - '0') <= 9 || (uint)((c | 0x20) - 'a') <= 5;
}
