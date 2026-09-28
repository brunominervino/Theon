namespace Theon.Schemas;

/// <summary>
/// Binds one property of <typeparamref name="T"/> to the schema that validates it.
/// </summary>
/// <typeparam name="T">The object type being validated.</typeparam>
/// <remarks>
/// The non-generic base exists so that an object schema can hold fields of many different types in
/// one array. The value types are erased here and nowhere else.
/// </remarks>
internal abstract class FieldBinding<T>
{
    internal abstract string Name { get; }

    internal abstract void Run(ref ParseContext context, T instance);
}

/// <summary>
/// Reads one property through a plain delegate and validates it.
/// </summary>
/// <typeparam name="T">The object type being validated.</typeparam>
/// <typeparam name="TValue">The property type.</typeparam>
/// <typeparam name="TParsed">The type the field schema produces.</typeparam>
/// <remarks>
/// The accessor is a <see cref="Func{T, TResult}"/>, not an <c>Expression</c>. A compiled lambda
/// is a direct call the JIT can inline, costs nothing at start-up, and survives trimming and
/// Native AOT untouched. An expression tree would have to be either interpreted on every read or
/// compiled through <c>Reflection.Emit</c>, which is exactly what AOT forbids. The property name,
/// which is the only thing the expression tree was ever wanted for, is captured at the call site
/// by the compiler instead. See <c>docs/decisions/0003-field-access.md</c>.
/// </remarks>
internal sealed class FieldBinding<T, TValue, TParsed>(
    string name,
    Func<T, TValue> accessor,
    Schema<TValue, TParsed> schema) : FieldBinding<T>
{
    internal override string Name => name;

    internal override void Run(ref ParseContext context, T instance)
    {
        context.PushProperty(name);
        schema.TryParse(ref context, accessor(instance), out _);
        context.Pop();
    }
}
