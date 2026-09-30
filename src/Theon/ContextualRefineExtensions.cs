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
}
