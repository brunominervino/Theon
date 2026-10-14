using Theon.Checks;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="bool"/>.
/// </summary>
/// <remarks>
/// <para>
/// There is nothing to validate about the value: a <see cref="bool"/> is one of two things and the
/// type system has already established which. This exists for everything around the value. A boolean
/// property needs a schema to be bound to a field at all, and without one an object holding a boolean
/// could not be described — the generated document had no entry for it, and a client generated from
/// that document did not know the field was there.
/// </para>
/// <para>
/// Demanding one particular value is <c>Theo.Literal(true)</c>, which already says exactly that and
/// writes <c>const: true</c>. There is deliberately no <c>IsTrue</c> here: a second way to say the
/// same thing costs every reader the question of which one to reach for.
/// </para>
/// </remarks>
public sealed class BooleanSchema : Schema<bool>
{
    private readonly Check<bool>[] _checks;

    internal BooleanSchema()
        : this([])
    {
    }

    private BooleanSchema(Check<bool>[] checks) => _checks = checks;

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    /// <example>
    /// <code>
    /// Theo.Boolean().Refine(accepted =&gt; accepted, "You have to agree to continue.");
    /// </code>
    /// </example>
    public BooleanSchema Refine(Func<bool, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new BooleanSchema([.. _checks, new RefineCheck<bool>(predicate, message)]);
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    /// <remarks>
    /// What a <see cref="bool"/> property declared <c>bool?</c> needs, and the reason a three-state
    /// checkbox is expressible without a type of its own.
    /// </remarks>
    public Schema<bool?> AllowNull() => new NullableValueSchema<bool>(this);

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.Boolean, _checks);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, bool input, out bool output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        foreach (var check in _checks)
        {
            if (context.ShouldStop)
            {
                break;
            }

            check.Run(ref context, ref output);
        }

        return context.ErrorCount == errorsBefore;
    }
}
