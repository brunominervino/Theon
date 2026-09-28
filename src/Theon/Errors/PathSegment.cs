using System.Diagnostics;

namespace Theon.Errors;

/// <summary>
/// One step in the path from the root of a parsed value to the place an error occurred:
/// either a property name or a collection index.
/// </summary>
/// <remarks>
/// Segments are kept structured rather than pre-rendered into a string so that a consumer which
/// needs structure (a JSON pointer, a form-field key, a <c>ProblemDetails</c> extension) does not
/// pay for a string it will only take apart again. Call <see cref="ValidationPath.ToString()"/>
/// when a human-readable path is actually wanted.
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct PathSegment : IEquatable<PathSegment>
{
    private readonly string? _name;
    private readonly int _index;

    private PathSegment(string? name, int index)
    {
        _name = name;
        _index = index;
    }

    /// <summary>Creates a segment addressing the property <paramref name="name"/>.</summary>
    /// <param name="name">The property name. Must not be <see langword="null"/>.</param>
    public static PathSegment Property(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new PathSegment(name, 0);
    }

    /// <summary>Creates a segment addressing the element at <paramref name="index"/>.</summary>
    /// <param name="index">The zero-based element index.</param>
    public static PathSegment Index(int index) => new(null, index);

    /// <summary>Gets a value indicating whether this segment addresses a collection element.</summary>
    public bool IsIndex => _name is null;

    /// <summary>Gets the property name, or <see langword="null"/> when <see cref="IsIndex"/> is <see langword="true"/>.</summary>
    public string? Name => _name;

    /// <summary>Gets the element index. Meaningful only when <see cref="IsIndex"/> is <see langword="true"/>.</summary>
    public int ElementIndex => _index;

    /// <inheritdoc />
    public bool Equals(PathSegment other) =>
        string.Equals(_name, other._name, StringComparison.Ordinal) && _index == other._index;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PathSegment other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_name, _index);

    /// <summary>Compares two segments for equality.</summary>
    public static bool operator ==(PathSegment left, PathSegment right) => left.Equals(right);

    /// <summary>Compares two segments for inequality.</summary>
    public static bool operator !=(PathSegment left, PathSegment right) => !left.Equals(right);

    /// <summary>Returns <c>Name</c> for a property segment and <c>[index]</c> for an index segment.</summary>
    public override string ToString() =>
        _name ?? string.Concat("[", _index.ToString(System.Globalization.CultureInfo.InvariantCulture), "]");
}
