using System.Globalization;
using System.Numerics;
using Theon.Checks;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates any numeric type: <see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>,
/// <see cref="double"/> and anything else implementing <see cref="INumber{TSelf}"/>.
/// </summary>
/// <typeparam name="T">The numeric type validated by this schema.</typeparam>
/// <remarks>
/// <para>
/// One generic schema covers every numeric type, because .NET can express the idea of being a
/// number as a constraint. A library without generic math has to write the same bounds logic once
/// per type and then keep the copies in step.
/// </para>
/// <para>
/// Values that are not finite are rejected before any bound is considered. For
/// <see cref="double"/> and <see cref="float"/> that means NaN and the infinities fail with
/// <see cref="ValidationErrorCode.InvalidType"/>. Comparing NaN against a bound reports false in
/// both directions, so without this test a NaN would slip through every range check written
/// against it. For integral types the test is a constant and the JIT removes it.
/// </para>
/// </remarks>
public sealed class NumberSchema<T> : Schema<T>
    where T : struct, INumber<T>
{
    private readonly Check<T>[] _checks;

    internal NumberSchema()
        : this([])
    {
    }

    private NumberSchema(Check<T>[] checks) => _checks = checks;

    private NumberSchema<T> With(Check<T> check) => new([.. _checks, check]);

    /// <summary>Requires the value to be greater than or equal to <paramref name="minimum"/>.</summary>
    /// <param name="minimum">The smallest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> Min(T minimum, string? message = null) =>
        With(new GreaterThanCheck<T>(minimum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be less than or equal to <paramref name="maximum"/>.</summary>
    /// <param name="maximum">The largest allowed value.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> Max(T maximum, string? message = null) =>
        With(new LessThanCheck<T>(maximum, inclusive: true) { Message = message });

    /// <summary>Requires the value to be strictly greater than <paramref name="bound"/>.</summary>
    /// <param name="bound">The exclusive lower bound.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> GreaterThan(T bound, string? message = null) =>
        With(new GreaterThanCheck<T>(bound, inclusive: false) { Message = message });

    /// <summary>Requires the value to be strictly less than <paramref name="bound"/>.</summary>
    /// <param name="bound">The exclusive upper bound.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> LessThan(T bound, string? message = null) =>
        With(new LessThanCheck<T>(bound, inclusive: false) { Message = message });

    /// <summary>Requires the value to be greater than zero.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> Positive(string? message = null) => GreaterThan(T.Zero, message);

    /// <summary>Requires the value to be zero or greater.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> NonNegative(string? message = null) => Min(T.Zero, message);

    /// <summary>Requires the value to be less than zero.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> Negative(string? message = null) => LessThan(T.Zero, message);

    /// <summary>Requires the value to be zero or less.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    public NumberSchema<T> NonPositive(string? message = null) => Max(T.Zero, message);

    /// <summary>Requires the value to be an exact multiple of <paramref name="divisor"/>.</summary>
    /// <param name="divisor">The divisor. Must not be zero.</param>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// The test is an exact remainder. For binary floating-point types that is the IEEE-754
    /// remainder, under which 0.3 is not a multiple of 0.1 because neither value is exactly
    /// representable. Use <see cref="decimal"/> when the multiples are decimal ones, such as money.
    /// </remarks>
    public NumberSchema<T> MultipleOf(T divisor, string? message = null)
    {
        if (divisor == T.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor), "The divisor must not be zero.");
        }

        return With(new MultipleOfCheck<T>(divisor) { Message = message });
    }

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public NumberSchema<T> Refine(Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<T>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<T?> AllowNull() => new NullableValueSchema<T>(this);

    internal override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKinds.For(typeof(T)), _checks);

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        if (!T.IsFinite(input))
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "a finite number",
                Received = T.IsNaN(input) ? "NaN" : input.ToString(null, CultureInfo.InvariantCulture),
            });

            return false;
        }

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
