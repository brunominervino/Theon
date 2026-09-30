namespace Theon;

/// <summary>
/// A schema that validates a value of type <typeparamref name="T"/> without changing its type.
/// </summary>
/// <typeparam name="T">The type validated by this schema.</typeparam>
/// <remarks>
/// This is the shape almost every schema has. It exists so that the common case reads as
/// <c>Schema&lt;string&gt;</c> rather than <c>Schema&lt;string, string&gt;</c>, while still being
/// usable anywhere a <see cref="Schema{TInput, TOutput}"/> is expected.
/// </remarks>
public abstract class Schema<T> : Schema<T, T>
{
    /// <summary>
    /// Produces a schema that requires both this one and <paramref name="other"/> to be satisfied.
    /// </summary>
    /// <param name="other">The second schema. Sees the same input this one does.</param>
    /// <remarks>
    /// <para>
    /// An intersection, not a pipeline. Both schemas are given the same value and both report, so a
    /// caller sees every reason the value was refused at once. That is the point: the two schemas were
    /// written independently — a platform rule and a tenant's extra rule, a shared base and one
    /// endpoint's additions — and neither is the input to the other. To feed one schema's result into
    /// the next, use <see cref="Schema{TInput, TOutput}.Transform{TNext}(Func{TOutput, TNext})"/>, which says so
    /// in its type.
    /// </para>
    /// <para>
    /// The value this produces is the left schema's, because only one of the two can be kept. Put a
    /// schema that normalizes — one that trims or lowercases — on the left, where its work survives.
    /// </para>
    /// <para>
    /// Chains, so three or more combine as <c>a.And(b).And(c)</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// private static readonly Schema&lt;string&gt; PlatformRules = Theo.String().Trim().MaxLength(200);
    /// private static readonly Schema&lt;string&gt; TenantRules = Theo.String().StartsWith("acme-");
    ///
    /// Schema&lt;string&gt; both = PlatformRules.And(TenantRules);
    /// </code>
    /// </example>
    public Schema<T> And(Schema<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Schemas.IntersectionSchema<T>(this, other);
    }
}
