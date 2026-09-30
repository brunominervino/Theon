using Theon.Errors;

using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a closed hierarchy by dispatching on the value's own type.
/// </summary>
/// <typeparam name="TBase">The base type or interface every value belongs to.</typeparam>
/// <remarks>
/// <para>
/// A discriminated union, written the way C# already writes one. Zod's version reads a literal
/// property because TypeScript erased the type and the discriminator therefore has to be data. Here
/// the discriminator <em>is</em> the type: the compiler proved each branch belongs to the hierarchy,
/// and <c>System.Text.Json</c> had already chosen which subtype to construct before any schema ran.
/// So the branch is chosen by a type test, which is faster than trying each alternative in turn and
/// gives a far better error — "PixPayment: the key is missing" rather than "not any of the three".
/// </para>
/// <para>
/// Contrast <see cref="Theo.OneOf"/>, which tries alternatives in order and reports one message for
/// all of them. That is the right tool when the alternatives are different <em>shapes of the same
/// type</em> — an identifier that may be an address or a phone number. This one is for different
/// types.
/// </para>
/// <para>
/// Where the model is instead one flat class carrying a <c>Kind</c> property, the rule you want is
/// <c>When</c>, which already applies a block of field rules on a condition. Nothing new is needed
/// for that case, and reaching for a type hierarchy you do not have would be worse.
/// </para>
/// <para>
/// Branches are tested in the order they were declared, and a branch that no value could ever reach
/// — because an earlier one already covers its type — is rejected when the schema is built rather
/// than left to be discovered. A value matching no branch is an error, so the union is closed.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// private static readonly Schema&lt;Payment&gt; PaymentSchema =
///     Theo.Subtypes&lt;Payment&gt;()
///         .Case(Theo.Object&lt;PixPayment&gt;()
///             .Field(x =&gt; x.Key, Theo.String().NotEmpty()))
///         .Case(Theo.Object&lt;CardPayment&gt;()
///             .Field(x =&gt; x.Number, Theo.String().Length(16))
///             .Field(x =&gt; x.Holder, Theo.String().NotEmpty()));
/// </code>
/// </example>
public sealed class SubtypesSchema<TBase> : Schema<TBase>
    where TBase : class
{
    private readonly SubtypeCase<TBase>[] _cases;
    private readonly string? _message;

    internal SubtypesSchema()
        : this([], null)
    {
    }

    private SubtypesSchema(SubtypeCase<TBase>[] cases, string? message)
    {
        _cases = cases;
        _message = message;
    }

    /// <summary>Adds the branch for <typeparamref name="TDerived"/>.</summary>
    /// <typeparam name="TDerived">The subtype this branch governs.</typeparam>
    /// <param name="schema">The schema values of that subtype must satisfy.</param>
    /// <remarks>
    /// <typeparamref name="TDerived"/> is inferred from <paramref name="schema"/>, so an object
    /// schema for the subtype is all that has to be written.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TDerived"/> already has a branch, or an earlier branch already matches
    /// every value of it, which would leave this one unreachable.
    /// </exception>
    public SubtypesSchema<TBase> Case<TDerived>(Schema<TDerived, TDerived> schema)
        where TDerived : class, TBase
    {
        ArgumentNullException.ThrowIfNull(schema);

        foreach (var existing in _cases)
        {
            if (existing.Subtype == typeof(TDerived))
            {
                throw new ArgumentException(
                    $"{typeof(TDerived).Name} already has a branch. Combine the two schemas with And " +
                    "rather than declaring the same subtype twice.",
                    nameof(schema));
            }

            // Branches are tried in order, so a branch for a type an earlier branch already accepts
            // can never run. Saying so here beats leaving a rule that silently never fires.
            if (existing.Subtype.IsAssignableFrom(typeof(TDerived)))
            {
                throw new ArgumentException(
                    $"{typeof(TDerived).Name} would never be reached, because the branch for " +
                    $"{existing.Subtype.Name} is declared first and already matches it. Declare the " +
                    "more derived type first.",
                    nameof(schema));
            }
        }

        return new SubtypesSchema<TBase>(
            [.. _cases, new SubtypeCase<TBase, TDerived>(schema)],
            _message);
    }

    /// <summary>Replaces the message reported when a value belongs to no branch.</summary>
    /// <param name="message">The message to report.</param>
    public SubtypesSchema<TBase> WithMessage(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new SubtypesSchema<TBase>(_cases, message);
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<TBase?> AllowNull() => new NullableReferenceSchema<TBase>(this);

    // A union of types becomes a union of schemas. anyOf and not oneOf: the branch is chosen by type
    // here, so at most one can ever match, and oneOf would ask a validator to prove the others fail to
    // reach an answer it already has.
    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = new SchemaDescription { AnyOf = [] };

        foreach (var branch in _cases)
        {
            description.AnyOf.Add(branch.Describe(context));
        }

        return description;
    }

    /// <inheritdoc />
    public override async ValueTask<ParseOutcome<TBase>> TryParseAsync(
        AsyncParseContext context,
        TBase input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(NullError(), _message);
            return new ParseOutcome<TBase>(false, default!);
        }

        foreach (var branch in _cases)
        {
            if (!branch.Matches(input))
            {
                continue;
            }

            await branch.RunAsync(context, input).ConfigureAwait(false);
            return context.ErrorCount == errorsBefore
                ? new ParseOutcome<TBase>(true, input)
                : new ParseOutcome<TBase>(false, default!);
        }

        context.AddError(NoBranchError(input), _message);
        return new ParseOutcome<TBase>(false, default!);
    }

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, TBase input, out TBase output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        if (input is null)
        {
            context.AddError(NullError(), _message);
            return false;
        }

        foreach (var branch in _cases)
        {
            if (!branch.Matches(input))
            {
                continue;
            }

            branch.Run(ref context, input);
            return context.ErrorCount == errorsBefore;
        }

        context.AddError(NoBranchError(input), _message);
        return false;
    }

    private static ValidationErrorInfo NullError() => new()
    {
        Code = ValidationErrorCode.InvalidType,
        Expected = typeof(TBase).Name,
        Received = "null",
    };

    // GetType is on the error path only, and naming what arrived is most of what makes this error
    // better than the one a list of alternatives can give. Type.Name of an instance you are holding
    // needs no metadata that trimming would have removed.
    private ValidationErrorInfo NoBranchError(TBase input) => new()
    {
        Code = ValidationErrorCode.InvalidType,
        Expected = string.Join(", ", _cases.Select(static branch => branch.Subtype.Name)),
        Received = input.GetType().Name,
    };
}
