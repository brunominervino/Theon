using System.Diagnostics;
using System.Text;

namespace Theon.Errors;

/// <summary>
/// The location of an error within the value that was parsed, as an ordered list of
/// <see cref="PathSegment"/> values.
/// </summary>
/// <remarks>
/// A path is only ever materialised when an error is actually produced. Parsing a valid value
/// allocates no path at all.
/// </remarks>
/// <example>
/// A failure on the postcode of the fourth user renders as <c>users[3].address.zipCode</c>.
/// </example>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct ValidationPath : IEquatable<ValidationPath>
{
    private readonly PathSegment[]? _segments;

    internal ValidationPath(PathSegment[]? segments) => _segments = segments;

    /// <summary>Gets the path referring to the parsed value itself, with no segments.</summary>
    public static ValidationPath Root => default;

    /// <summary>Gets a value indicating whether this path refers to the root value.</summary>
    public bool IsRoot => _segments is null || _segments.Length == 0;

    /// <summary>Gets the number of segments in this path.</summary>
    public int Length => _segments?.Length ?? 0;

    /// <summary>Gets the segments of this path, outermost first.</summary>
    public ReadOnlySpan<PathSegment> Segments => _segments ?? ReadOnlySpan<PathSegment>.Empty;

    /// <inheritdoc />
    public bool Equals(ValidationPath other) => Segments.SequenceEqual(other.Segments);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValidationPath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var segment in Segments)
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }

    /// <summary>Compares two paths for equality.</summary>
    public static bool operator ==(ValidationPath left, ValidationPath right) => left.Equals(right);

    /// <summary>Compares two paths for inequality.</summary>
    public static bool operator !=(ValidationPath left, ValidationPath right) => !left.Equals(right);

    /// <summary>
    /// Renders the path in the conventional dotted form, with indices in brackets and no
    /// leading dot, for example <c>users[3].address.zipCode</c>. The root path renders as
    /// an empty string.
    /// </summary>
    public override string ToString()
    {
        var segments = Segments;
        if (segments.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            if (segment.IsIndex)
            {
                builder.Append('[').Append(segment.ElementIndex).Append(']');
            }
            else
            {
                if (builder.Length > 0)
                {
                    builder.Append('.');
                }

                builder.Append(segment.Name);
            }
        }

        return builder.ToString();
    }
}
