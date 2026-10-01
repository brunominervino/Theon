using System.Runtime.CompilerServices;
using Theon.Checks;
using Theon.Errors;

using Theon.Metadata;

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
/// <para>
/// <typeparamref name="T"/> may be a class, a record, a struct or a record struct. Accepting
/// <see langword="null"/> is offered as an extension method rather than a member, because the
/// reference and value cases need different wrappers and a member cannot constrain the type
/// parameter its own class declared.
/// </para>
/// <para>
/// An accessor is a <see cref="Func{T, TResult}"/>, so a value type is copied once per field read.
/// Measured against the same four fields, a struct and a class parse within noise of each other
/// and neither allocates: validation work dominates the copy at any ordinary struct size. A
/// deliberately large struct would not be free, and the benchmark to check that lives in
/// <c>benchmarks/</c>.
/// </para>
/// </remarks>
public sealed class ObjectSchema<T> : Schema<T>
    where T : notnull
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

    /// <summary>
    /// Requires something of the object, but only when a condition holds.
    /// </summary>
    /// <typeparam name="TValue">The type of the property the error is reported against.</typeparam>
    /// <param name="condition">When this returns <see langword="true"/>, the requirement applies.</param>
    /// <param name="requirement">What must then be true of the object.</param>
    /// <param name="path">Names the property the error belongs to.</param>
    /// <param name="message">The message to report when the requirement is not met.</param>
    /// <param name="pathExpression">Supplied by the compiler.</param>
    /// <remarks>
    /// The same rule can be written as a single <c>Refine</c> over
    /// <c>!condition || requirement</c>, and that is a material implication spelled as a
    /// disjunction: correct, and misread by almost everyone almost every time. Two predicates say
    /// what is meant.
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Object&lt;TaskItem&gt;()
    ///     .Field(x =&gt; x.Status, Theo.Enum&lt;TaskStatus&gt;())
    ///     .When(
    ///         x =&gt; x.Status == TaskStatus.Completed,
    ///         x =&gt; x.CompletedAt is not null,
    ///         x =&gt; x.CompletedAt,
    ///         "A completion date is required once the task is completed.");
    /// </code>
    /// </example>
    public ObjectSchema<T> When<TValue>(
        Func<T, bool> condition,
        Func<T, bool> requirement,
        Func<T, TValue> path,
        string message,
        [CallerArgumentExpression(nameof(path))] string? pathExpression = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(message);

        var name = MemberName.From(pathExpression, nameof(path));
        return new ObjectSchema<T>(
            _fields,
            [.. _checks, new ConditionalRefineCheck<T>(condition, requirement, name, message)]);
    }

    /// <summary>
    /// Requires something of the object, but only when a condition holds, reporting against the
    /// object itself rather than a named property.
    /// </summary>
    /// <param name="condition">When this returns <see langword="true"/>, the requirement applies.</param>
    /// <param name="requirement">What must then be true of the object.</param>
    /// <param name="message">The message to report when the requirement is not met.</param>
    public ObjectSchema<T> When(Func<T, bool> condition, Func<T, bool> requirement, string message)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentException.ThrowIfNullOrEmpty(message);

        return new ObjectSchema<T>(
            _fields,
            [.. _checks, new ConditionalRefineCheck<T>(condition, requirement, null, message)]);
    }

    /// <summary>
    /// Applies a whole set of rules, but only when a condition holds.
    /// </summary>
    /// <param name="condition">When this returns <see langword="true"/>, the rules apply.</param>
    /// <param name="rules">
    /// Builds the rules from an empty schema for the same type. Fields validated here are validated
    /// in addition to those declared outside the block.
    /// </param>
    /// <remarks>
    /// This is the shape a real conditional usually has. A status reaching some value rarely
    /// unlocks one requirement; it unlocks three, and writing them as three guarded predicates
    /// restates the same condition three times and invites the copies to drift apart.
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Object&lt;TaskItem&gt;()
    ///     .Field(x =&gt; x.Status, Theo.Enum&lt;TaskStatus&gt;())
    ///     .When(x =&gt; x.Status == TaskStatus.Completed, rules =&gt; rules
    ///         .Field(x =&gt; x.CompletedAt, Theo.DateTime().RequireUtc().Required())
    ///         .Field(x =&gt; x.ClosedBy, Theo.String().NotEmpty()));
    /// </code>
    /// </example>
    public ObjectSchema<T> When(Func<T, bool> condition, Func<ObjectSchema<T>, ObjectSchema<T>> rules)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(rules);

        var inner = rules(new ObjectSchema<T>())
            ?? throw new ArgumentException("The rules builder returned null.", nameof(rules));

        return new ObjectSchema<T>(_fields, [.. _checks, new ConditionalSchemaCheck<T>(condition, inner)]);
    }

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = CheckDescription.Build(SchemaKind.Object, _checks);
        var properties = new List<PropertyDescription>(_fields.Length);

        foreach (var field in _fields)
        {
            properties.Add(field.Describe(context));
        }

        description.Properties = properties;
        return description.ToDescription();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Mirrors the synchronous order exactly, so a schema behaves the same whichever way it is
    /// parsed. Fields are awaited one at a time rather than in parallel: running them together
    /// would turn one slow lookup per request into several concurrent ones, and the errors are
    /// reported in declaration order, which a caller can rely on.
    /// </remarks>
    public override async ValueTask<ParseOutcome<T>> TryParseAsync(AsyncParseContext context, T input)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = typeof(T).Name,
                Received = "null",
            });

            return new ParseOutcome<T>(false, default!);
        }

        foreach (var field in _fields)
        {
            if (context.ShouldStop)
            {
                break;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            await field.RunAsync(context, input).ConfigureAwait(false);
        }

        if (context.ErrorCount != errorsBefore)
        {
            return new ParseOutcome<T>(false, default!);
        }

        // Object-level rules are synchronous; borrow a context positioned at the current path.
        var sync = context.BeginSync();
        var value = input;
        try
        {
            foreach (var check in _checks)
            {
                if (sync.ShouldStop)
                {
                    break;
                }

                check.Run(ref sync, ref value);
            }
        }
        finally
        {
            context.EndSync(ref sync);
        }

        return context.ErrorCount == errorsBefore
            ? new ParseOutcome<T>(true, value)
            : new ParseOutcome<T>(false, default!);
    }

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

        // Always false for a value type, and the JIT removes the branch there entirely.
        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = typeof(T).Name,
                Received = "null",
            });

            output = default!;
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
