using System.ComponentModel;
using Theon.Metadata;

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

    /// <summary>
    /// Runs this schema asynchronously, recording any failures into <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The parse in progress. Errors are recorded here.</param>
    /// <param name="input">The value to parse.</param>
    /// <remarks>
    /// The default implementation runs the synchronous path, which is correct for every schema that
    /// has no asynchronous rule in it — which is nearly all of them. A schema only overrides this
    /// when it genuinely has to await something, and composite schemas override it to carry the
    /// asynchrony through to their children.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public virtual ValueTask<ParseOutcome<TOutput>> TryParseAsync(AsyncParseContext context, TInput input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var succeeded = context.RunSynchronously(this, input, out var output);
        return new ValueTask<ParseOutcome<TOutput>>(new ParseOutcome<TOutput>(succeeded, output));
    }

    /// <summary>
    /// Parses <paramref name="input"/> asynchronously, throwing if it does not satisfy this schema.
    /// </summary>
    /// <param name="input">The value to parse.</param>
    /// <param name="options">Options for this call, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels any I/O the rules perform.</param>
    /// <exception cref="SchemaValidationException">The value did not satisfy this schema.</exception>
    public async ValueTask<TOutput> ParseAsync(
        TInput input,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SafeParseAsync(input, options, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new SchemaValidationException(result.Errors);
    }

    /// <summary>
    /// Parses <paramref name="input"/> asynchronously, returning the errors rather than throwing.
    /// </summary>
    /// <param name="input">The value to parse.</param>
    /// <param name="options">Options for this call, or <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancels any I/O the rules perform.</param>
    /// <remarks>
    /// Safe to call on a schema with no asynchronous rules; it simply never awaits anything.
    /// </remarks>
    public async ValueTask<ParseResult<TOutput>> SafeParseAsync(
        TInput input,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var context = new AsyncParseContext(options ?? ParseOptions.Default, cancellationToken);
        var outcome = await TryParseAsync(context, input).ConfigureAwait(false);

        return outcome.Succeeded && !context.HasErrors
            ? new ParseResult<TOutput>(outcome.Value)
            : new ParseResult<TOutput>(context.TakeErrors());
    }

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
        return new Schemas.TransformSchema<TInput, TOutput, TNext>(this, transform, then: null);
    }

    /// <summary>
    /// Produces a schema that runs this one, maps the result to another type, and then validates what
    /// it mapped to.
    /// </summary>
    /// <typeparam name="TNext">The type to map to.</typeparam>
    /// <param name="transform">The mapping. Runs only when this schema succeeded.</param>
    /// <param name="then">The schema the mapped value must satisfy.</param>
    /// <remarks>
    /// <para>
    /// A transformation used to have to be the last thing in a chain, which left the most ordinary
    /// pipeline there is unsayable: text arrives, becomes a number, and the number has bounds. Every
    /// query string, form field and configuration value is that shape.
    /// </para>
    /// <para>
    /// Errors from <paramref name="then"/> land at the same path as the value that produced them, so a
    /// page size out of range reports against <c>PageSize</c> and not against the number it became.
    /// </para>
    /// <para>
    /// A generated document cannot express this, and records that it could not: the document describes
    /// what a caller sends, and the dialect has no way to state a bound on what this program made of
    /// it. Ask <c>ToJsonSchema</c> to report what it had to leave out if that matters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Schema&lt;string, int&gt; pageSize = Theo.String().Trim()
    ///     .TryTransform&lt;int&gt;(int.TryParse, "Must be a whole number.", Theo.Int().Min(1).Max(100));
    /// </code>
    /// </example>
    public Schema<TInput, TNext> Transform<TNext>(Func<TOutput, TNext> transform, Schema<TNext> then)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(then);
        return new Schemas.TransformSchema<TInput, TOutput, TNext>(this, transform, then);
    }

    /// <summary>
    /// Produces a schema that runs this one and then converts the result, reporting a failed
    /// conversion rather than throwing.
    /// </summary>
    /// <typeparam name="TNext">The type to convert to.</typeparam>
    /// <param name="attempt">
    /// The conversion, shaped like <c>TryParse</c> so that <c>int.TryParse</c> and its relatives can
    /// be handed over as they are.
    /// </param>
    /// <param name="message">The message to report when the conversion fails.</param>
    /// <param name="then">
    /// An optional schema the converted value must satisfy, checked only once the conversion
    /// succeeded.
    /// </param>
    /// <remarks>
    /// <para>
    /// <see cref="Transform{TNext}(Func{TOutput, TNext})"/> takes a mapping that cannot fail, which
    /// rules out the mapping most often wanted: text into a number. <c>int.Parse</c> throws, and an
    /// exception is the wrong answer to a value a person typed wrongly. This reports instead, at the
    /// path the value came from, alongside every other error in the same parse.
    /// </para>
    /// <para>
    /// The conversion runs only after this schema passed, so it never sees a value already known to be
    /// unacceptable.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // A page size that arrives as text, becomes a number, and then has to be in range.
    /// Theo.String().Trim()
    ///     .TryTransform&lt;int&gt;(int.TryParse, "Must be a whole number.", Theo.Int().Min(1).Max(100));
    /// </code>
    /// </example>
    public Schema<TInput, TNext> TryTransform<TNext>(
        TransformAttempt<TOutput, TNext> attempt,
        string message,
        Schema<TNext>? then = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new Schemas.TryTransformSchema<TInput, TOutput, TNext>(this, attempt, message, then);
    }

    /// <summary>
    /// Produces a schema that never fails: where this one rejects the value, the result reports
    /// nothing and answers with <paramref name="fallback"/>.
    /// </summary>
    /// <param name="fallback">The value to produce when this schema rejects the input.</param>
    /// <remarks>
    /// <para>
    /// For an input you would rather interpret than argue with: a sort order from a query string, a
    /// feature flag from a header, a stale value in a configuration file. Errors the inner schema
    /// raised are discarded, not merged, so nothing downstream sees a complaint about a value that
    /// was replaced.
    /// </para>
    /// <para>
    /// A rejected value is swallowed; an exception is not. A refinement that threw, or an
    /// asynchronous rule reached from the synchronous path, is a defect in the program rather than
    /// something a person typed wrongly, and a fallback that hid it would turn a loud failure at the
    /// first call into a wrong answer for ever.
    /// </para>
    /// <para>
    /// Like <c>Default</c>, this shapes the value a parse produces, so it belongs where that value is
    /// read — a top-level parse, or a step in a chain. An object schema validates an instance without
    /// rebuilding it, so a fallback on a field would silence the error and change nothing else.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // An unrecognised sort order is not worth a 400.
    /// var order = Theo.Enum&lt;SortOrder&gt;().Catch(SortOrder.Ascending).Parse(parsed);
    /// </code>
    /// </example>
    public Schema<TInput, TOutput> Catch(TOutput fallback) =>
        new Schemas.CatchSchema<TInput, TOutput>(this, fallback);

    /// <summary>
    /// Produces a schema that carries documentation alongside the rules, for generated documents.
    /// </summary>
    /// <param name="title">A short name for the value.</param>
    /// <param name="description">A sentence or two about what the value means.</param>
    /// <param name="example">A value worth showing a reader.</param>
    /// <param name="deprecated">Whether callers should stop using it.</param>
    /// <remarks>
    /// <para>
    /// Annotations do not validate anything. They exist so that a document generated by
    /// <c>ToJsonSchema</c> can say what the rules cannot: why a field exists, what a
    /// <c>Refine</c> is checking, which of several acceptable forms is preferred.
    /// </para>
    /// <para>
    /// Kept on the schema rather than in a table on the side. A schema is immutable and is built once
    /// at start-up, so a wrapper costs one delegating call on a schema that opted in and nothing at
    /// all on one that did not — where a registry keyed on instances would mean global mutable state
    /// and a lifetime to reason about.
    /// </para>
    /// <para>
    /// Annotating produces a plain schema, so it goes last in a chain, the way <c>Catch</c> and
    /// <c>Transform</c> do. Calling it with nothing to say returns the same schema.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.String().Email().Annotate(
    ///     description: "Where we write to you. Never shown to other people.",
    ///     example: "ada@example.com");
    /// </code>
    /// </example>
    public Schema<TInput, TOutput> Annotate(
        string? title = null,
        string? description = null,
        object? example = null,
        bool deprecated = false)
    {
        if (title is null && description is null && example is null && !deprecated)
        {
            return this;
        }

        return new Schemas.AnnotatedSchema<TInput, TOutput>(this, title, description, example, deprecated);
    }

    // Reifies this schema's structure, so that a document can be generated without asking what kind
    // of schema it is holding. The default says nothing, which is the honest answer for a schema this
    // assembly does not know: the hook is internal, so a custom schema from elsewhere cannot describe
    // itself, and a document that omits a constraint is incomplete where one that invents a type
    // would be wrong.
    internal virtual SchemaDescription Describe(DescriptionContext context) =>
        new() { Kind = SchemaKind.Unknown };

    private static readonly ParseOptions FailFastOptions = new() { StopOnFirstError = true };
}
