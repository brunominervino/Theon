using System.Runtime.CompilerServices;
using Theon.Checks;
using Theon.Errors;

namespace Theon.Schemas;

/// <summary>
/// Validates an object of type <typeparamref name="T"/> property by property.
/// </summary>
/// <typeparam name="T">The object type validated by this schema.</typeparam>
/// <remarks>
/// <para>
/// This schema does not build <typeparamref name="T"/>, it checks one. Deserialization is already
/// solved in .NET and solved well, so the value arrives fully materialized and correctly shaped,
/// and what remains is whether the values inside it are acceptable. That is a smaller job than the
/// one a TypeScript validator has to do, and it is why there is nothing here about unknown keys or
/// missing properties: the type system settled both before the parse began.
/// </para>
/// <para>
/// The object itself is never mutated. A rule that rewrites a value, such as <c>Trim</c>, affects
/// the rules that follow it within that field, not the property on the instance.
/// </para>
/// </remarks>
public sealed class ObjectSchema<T> : Schema<T>
    where T : class
{
    private readonly FieldBinding<T>[] _fields;
    private readonly Check<T>[] _checks;

    internal ObjectSchema()
        : this([], [])
    {
    }

    private ObjectSchema(FieldBinding<T>[] fields, Check<T>[] checks)
    {
        _fields = fields;
        _checks = checks;
    }

    /// <summary>Validates one property with the given schema.</summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <typeparam name="TParsed">The type the field schema produces.</typeparam>
    /// <param name="accessor">Reads the property, written as <c>x =&gt; x.Email</c>.</param>
    /// <param name="schema">The schema the property must satisfy.</param>
    /// <param name="accessorExpression">
    /// Supplied by the compiler. The property name is read from the source text of
    /// <paramref name="accessor"/>, so nothing is inspected at run time.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="accessor"/> is not a simple property access and the name could not be read.
    /// Use <see cref="Field{TValue, TParsed}(string, Func{T, TValue}, Schema{TValue, TParsed})"/>.
    /// </exception>
    /// <example>
    /// <code>
    /// Theo.Object&lt;CreateUser&gt;()
    ///     .Field(x =&gt; x.Email, Theo.String().Trim().Email())
    ///     .Field(x =&gt; x.Age, Theo.Int().Min(18));
    /// </code>
    /// </example>
    public ObjectSchema<T> Field<TValue, TParsed>(
        Func<T, TValue> accessor,
        Schema<TValue, TParsed> schema,
        [CallerArgumentExpression(nameof(accessor))] string? accessorExpression = null)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(schema);

        return Field(MemberName.From(accessorExpression, nameof(accessor)), accessor, schema);
    }

    /// <summary>Validates one property with the given schema, under an explicit name.</summary>
    /// <typeparam name="TValue">The property type.</typeparam>
    /// <typeparam name="TParsed">The type the field schema produces.</typeparam>
    /// <param name="name">The name to use in error paths.</param>
    /// <param name="accessor">Reads the property.</param>
    /// <param name="schema">The schema the property must satisfy.</param>
    /// <remarks>
    /// Use this when the accessor is not a plain property read, or when the name in error paths
    /// should differ from the property name, for example to match a JSON field.
    /// </remarks>
    public ObjectSchema<T> Field<TValue, TParsed>(
        string name,
        Func<T, TValue> accessor,
        Schema<TValue, TParsed> schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(schema);

        return new ObjectSchema<T>(
            [.. _fields, new FieldBinding<T, TValue, TParsed>(name, accessor, schema)],
            _checks);
    }

    /// <summary>Requires the object as a whole to satisfy a predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the object is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    /// <remarks>
    /// The error is reported against the object itself, with an empty path. Prefer the overload
    /// that names a property when there is one the user should go and correct.
    /// </remarks>
    public ObjectSchema<T> Refine(Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);

        return new ObjectSchema<T>(_fields, [.. _checks, new PathedRefineCheck<T>(predicate, null, message)]);
    }

    /// <summary>
    /// Requires the object as a whole to satisfy a predicate, reporting any failure against one
    /// named property.
    /// </summary>
    /// <typeparam name="TValue">The type of the property the error is reported against.</typeparam>
    /// <param name="predicate">Returns <see langword="true"/> when the object is acceptable.</param>
    /// <param name="path">Names the property the error belongs to, written as <c>x =&gt; x.Confirmation</c>.</param>
    /// <param name="message">The message to report when the predicate fails.</param>
    /// <param name="pathExpression">Supplied by the compiler.</param>
    /// <example>
    /// <code>
    /// Theo.Object&lt;SignUp&gt;()
    ///     .Field(x =&gt; x.Password, Theo.String().MinLength(8))
    ///     .Field(x =&gt; x.PasswordConfirmation, Theo.String())
    ///     .Refine(
    ///         x =&gt; x.Password == x.PasswordConfirmation,
    ///         x =&gt; x.PasswordConfirmation,
    ///         "The passwords do not match.");
    /// </code>
    /// </example>
    public ObjectSchema<T> Refine<TValue>(
        Func<T, bool> predicate,
        Func<T, TValue> path,
        string message,
        [CallerArgumentExpression(nameof(path))] string? pathExpression = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(message);

        var name = MemberName.From(pathExpression, nameof(path));
        return new ObjectSchema<T>(_fields, [.. _checks, new PathedRefineCheck<T>(predicate, name, message)]);
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<T?> AllowNull() => new NullableReferenceSchema<T>(this);

    /// <inheritdoc />
    /// <remarks>
    /// Object-level refinements run only once every field has validated. A rule comparing two
    /// properties is written against values that were supposed to be valid, and running it over
    /// values already known to be broken produces either a second, redundant complaint or a
    /// <see cref="NullReferenceException"/> from inside user code.
    /// </remarks>
    public override bool TryParse(ref ParseContext context, T input, out T output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = typeof(T).Name,
                Received = "null",
            });

            output = null!;
            return false;
        }

        output = input;

        foreach (var field in _fields)
        {
            if (context.ShouldStop)
            {
                break;
            }

            field.Run(ref context, input);
        }

        if (context.ErrorCount != errorsBefore)
        {
            return false;
        }

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
