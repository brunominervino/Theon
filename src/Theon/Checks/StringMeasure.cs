namespace Theon.Checks;

/// <summary>
/// Measures string length the way a user counts characters.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="string.Length"/> counts UTF-16 code units, so a single emoji counts as two and a
/// user who typed one character is told they typed two. Length bounds are therefore measured in
/// Unicode code points, where a surrogate pair counts once.
/// </para>
/// <para>
/// Code points, not grapheme clusters: an accented letter written as a base plus a combining mark
/// still counts as two. Grapheme segmentation is culture- and version-dependent, which would make
/// the same schema accept different strings on different machines. Code points are stable.
/// </para>
/// <para>
/// The comparisons below only count when they have to. A string whose UTF-16 length already
/// satisfies the bound cannot fail it, because the code-point count is never larger, so the common
/// case never walks the string at all.
/// </para>
/// </remarks>
internal static class StringMeasure
{
    /// <summary>Counts Unicode code points, treating an unpaired surrogate as one.</summary>
    internal static int CodePointCount(ReadOnlySpan<char> value)
    {
        var units = value.Length;
        if (!value.ContainsAnyInRange('\uD800', '\uDBFF'))
        {
            return units;
        }

        var count = units;
        for (var i = 0; i < units - 1; i++)
        {
            if (char.IsHighSurrogate(value[i]) && char.IsLowSurrogate(value[i + 1]))
            {
                count--;
                i++;
            }
        }

        return count;
    }

    /// <summary>Reports whether the code-point count is below <paramref name="minimum"/>.</summary>
    internal static bool IsShorterThan(ReadOnlySpan<char> value, int minimum)
    {
        var units = value.Length;
        if (units < minimum)
        {
            return true;
        }

        // Every code point costs at least one unit and at most two, so a string with twice the
        // required units cannot be short no matter how it is encoded.
        if (units >= (long)minimum * 2)
        {
            return false;
        }

        return CodePointCount(value) < minimum;
    }

    /// <summary>Reports whether the code-point count exceeds <paramref name="maximum"/>.</summary>
    internal static bool IsLongerThan(ReadOnlySpan<char> value, int maximum)
    {
        if (value.Length <= maximum)
        {
            return false;
        }

        return CodePointCount(value) > maximum;
    }

    /// <summary>Compares the code-point count against <paramref name="length"/>.</summary>
    /// <returns>A negative value if shorter, zero if equal, a positive value if longer.</returns>
    internal static int CompareLength(ReadOnlySpan<char> value, int length)
    {
        var units = value.Length;
        if (units < length)
        {
            return -1;
        }

        if (units == length)
        {
            return 0;
        }

        if (units > (long)length * 2)
        {
            return 1;
        }

        return CodePointCount(value).CompareTo(length);
    }
}
