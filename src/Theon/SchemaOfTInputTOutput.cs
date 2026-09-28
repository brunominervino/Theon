using System.ComponentModel;

namespace Theon;

/// <summary>
/// A rule for turning a value of type <typeparamref name="TInput"/> into a validated value of type
/// <typeparamref name="TOutput"/>, or into a structured explanation of why it cannot.
/// </summary>
/// <typeparam name="TInput">The type accepted by this schema.</typeparam>
/// <typeparam name="TOutput">The type produced by this schema.</typeparam>
/// <remarks>
/// <para>
/// Most schemas neither widen nor narrow the type and are written as <see cref="Schema{T}"/>, which
/// is simply this type with both parameters the same. The two parameters exist so that
/// <c>Transform</c> can be typed honestly: a schema that parses a string into an integer really is
/// a <c>Schema&lt;string, int&gt;</c>.
/// </para>
/// <para>
/// Schemas are immutable. Every builder method returns a new instance, so a schema may be built
/// once at start-up, stored in a static field, and used concurrently from any number of threads.
/// </para>
/// </remarks>
public abstract class Schema<TInput, TOutput>
{
    /// <summary>
    /// Runs this schema against <paramref name="input"/>, recording any failures into
    /// <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The parse in progress. Errors are recorded here.</param>
    /// <param name="input">The value to parse.</param>
    /// <param name="output">The parsed value on success; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if this schema produced a value.</returns>
    /// <remarks>
    /// This is the extension point for custom schemas, and the only method a derived type must
    /// implement. Application code should call <see cref="Parse"/> or <see cref="SafeParse"/>
    /// instead: they own the context and turn the result into something usable.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public abstract bool TryParse(ref ParseContext context, TInput input, out TOutput output);

    /// <summary>Parses <paramref name="input"/>, throwing if it does not satisfy this schema.</summary>
    /// <param name="input">The value to parse.</param>
    /// <param name="options">Options for this call, or <see langword="null"/> for the defaults.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="SchemaValidationException">The value did not satisfy this schema.</exception>
    public TOutput Parse(TInput input, ParseOptions? options = null)
    {
        var result = SafeParse(input, options);
        return result.IsSuccess ? result.Value! : throw new SchemaValidationException(result.Errors);
    }

    /// <summary>
    /// Parses <paramref name="input"/>, returning the errors rather than throwing when it does not
    /// satisfy this schema.
    /// </summary>
    /// <param name="input">The value to parse.</param>
    /// <param name="options">Options for this call, or <see langword="null"/> for the defaults.</param>
    public ParseResult<TOutput> SafeParse(TInput input, ParseOptions? options = null)
    {
        var context = new ParseContext(options ?? ParseOptions.Default);
        var succeeded = TryParse(ref context, input, out var output);

        return succeeded && !context.HasErrors
            ? new ParseResult<TOutput>(output)
            : new ParseResult<TOutput>(context.TakeErrors());
    }

    /// <summary>
    /// Reports whether <paramref name="input"/> satisfies this schema, without producing either a
    /// value or error messages.
    /// </summary>
    /// <param name="input">The value to check.</param>
    /// <remarks>
    /// Stops at the first failure, so this is the cheapest way to ask a yes-or-no question.
    /// </remarks>
    public bool IsValid(TInput input)
    {
        var context = new ParseContext(FailFastOptions);
        return TryParse(ref context, input, out _) && !context.HasErrors;
    }

    /// <summary>
    /// Produces a schema that runs this one and then maps the result to another type.
    /// </summary>
    /// <typeparam name="TNext">The type to map to.</typeparam>
    /// <param name="transform">The mapping. Runs only when this schema succeeded.</param>
    /// <remarks>
    /// This is where the two type parameters earn their keep: the result really is a schema from
    /// the original input type to the new one, and the signature says so.
    /// </remarks>
    /// <example>
    /// <code>
    /// Schema&lt;string, int&gt; port = Theo.String().Trim().Matches(DigitsOnly).Transform(int.Parse);
    /// </code>
    /// </example>
    public Schema<TInput, TNext> Transform<TNext>(Func<TOutput, TNext> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return new Schemas.TransformSchema<TInput, TOutput, TNext>(this, transform);
    }

    private static readonly ParseOptions FailFastOptions = new() { StopOnFirstError = true };
}
