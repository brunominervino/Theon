using Theon.Schemas;

namespace Theon;

/// <summary>
/// Adds a rule that reports its own failures.
/// </summary>
public static class ContextualRefineExtensions
{
    /// <summary>
    /// Requires the value to satisfy a rule that decides for itself what to report, and where.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="schema">The schema to extend.</param>
    /// <param name="rule">
    /// The rule. Reports nothing to accept the value, or calls <c>AddError</c> once per thing wrong.
    /// </param>
    /// <remarks>
    /// <para>
    /// The other <c>Refine</c> answers yes or no, and produces one error with the
    /// <c>Custom</c> code at the path the value is at. That is the right shape for most rules and
    /// cannot express the rest: a rule with two distinct complaints, a rule whose failure belongs
    /// against one particular property, a rule that wants a code a caller can branch on.
    /// </para>
    /// <para>
    /// Before this existed, the only way to do any of that was to implement a schema against
    /// <c>TryParse</c>, which is a great deal of ceremony for one rule.
    /// </para>
    /// <para>
    /// The rule runs only once everything before it has passed, so it never sees a value already known
    /// to be unacceptable.
    /// </para>
    /// <para>
    /// A generated document cannot express an arbitrary rule and records that it could not.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Two separate complaints about one password, each against the field the user has to fix.
    /// Theo.Object&lt;SignUp&gt;()
    ///     .Field(x =&gt; x.Password, Theo.String().MinLength(8))
    ///     .Refine(static (signUp, ref ParseContext context) =&gt;
    ///     {
    ///         if (signUp.Password != signUp.PasswordConfirmation)
    ///         {
    ///             context.PushProperty(nameof(signUp.PasswordConfirmation));
    ///             context.AddError(
    ///                 new ValidationErrorInfo { Code = ValidationErrorCode.Custom },
    ///                 "The two passwords do not match.");
    ///             context.Pop();
    ///         }
    ///
    ///         if (signUp.Password.Contains(signUp.Email, StringComparison.OrdinalIgnoreCase))
    ///         {
    ///             context.PushProperty(nameof(signUp.Password));
    ///             context.AddError(
    ///                 new ValidationErrorInfo { Code = ValidationErrorCode.Custom },
    ///                 "The password must not contain your address.");
    ///             context.Pop();
    ///         }
    ///     });
    /// </code>
    /// </example>
    public static Schema<T> Refine<T>(this Schema<T> schema, RefineRule<T> rule)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rule);

        return new ContextualRefinedSchema<T>(schema, rule);
    }

    /// <summary>
    /// Requires the value to satisfy a predicate, reporting a code of your own when it does not.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="schema">The schema to add the rule to.</param>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">
    /// The message to report when it is not, or <see langword="null"/> to let the message provider
    /// write it from <paramref name="code"/>.
    /// </param>
    /// <param name="code">
    /// A code of your own naming, such as <c>"cnpj.digito_verificador"</c>, reported beside the
    /// message.
    /// </param>
    /// <remarks>
    /// <para>
    /// The two-argument <c>Refine</c> on each schema reports
    /// <see cref="Errors.ValidationErrorCode.Custom"/>, which says a caller-supplied rule rejected the
    /// value and nothing about which one. Where several refinements sit on the same value and a caller
    /// has to tell them apart, this is how: the enum still says <c>Custom</c> and
    /// <see cref="Errors.ValidationError.CustomCode"/> says which.
    /// </para>
    /// <para>
    /// A message given here wins over every provider, which is the message chain decision 5 settled
    /// and this does not change. Pass <see langword="null"/> instead and the chain runs: a provider
    /// that recognises the code writes the sentence, which is how a localization package says one
    /// particular rule in its own language. Nothing answering it falls through to "Invalid input.",
    /// so a code with no message anywhere is a code nobody has written a sentence for yet.
    /// </para>
    /// <para>
    /// This produces a plain schema, so it goes last in a chain the way <c>Annotate</c> and
    /// <c>Catch</c> do. The two-argument form on each schema returns that schema's own type and can be
    /// followed by more of its rules.
    /// </para>
    /// <para>
    /// A generated document cannot express an arbitrary rule, and records that it could not.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // The sentence here, and it wins over every provider:
    /// Theo.String().Trim().Length(14).Refine(
    ///     IsValidCnpj, "Informe um CNPJ valido.", "cnpj.digito_verificador");
    ///
    /// // Or no sentence, and whichever provider recognises the code writes it:
    /// Theo.String().Trim().Length(14).Refine(
    ///     IsValidCnpj, message: null, code: "cnpj.digito_verificador");
    /// </code>
    /// </example>
    public static Schema<T> Refine<T>(
        this Schema<T> schema,
        Func<T, bool> predicate,
        string? message,
        string code)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(code);

        return schema.Refine((T value, ref ParseContext context) =>
        {
            if (predicate(value))
            {
                return;
            }

            context.AddError(
                new Errors.ValidationErrorInfo
                {
                    Code = Errors.ValidationErrorCode.Custom,
                    CustomCode = code,
                },
                message);
        });
    }
}
