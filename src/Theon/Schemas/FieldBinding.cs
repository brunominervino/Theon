using Theon.Metadata;

namespace Theon.Schemas;

// Binds one property of  to the schema that validates it.
// The object type being validated.
// The non-generic base exists so that an object schema can hold fields of many different types in
// one array. The value types are erased here and nowhere else.
internal abstract class FieldBinding<T>
{
    internal abstract string Name { get; }

    internal abstract void Run(ref ParseContext context, T instance);

    internal abstract ValueTask RunAsync(AsyncParseContext context, T instance);

    internal abstract PropertyDescription Describe(DescriptionContext context);
}

// Reads one property through a plain delegate and validates it.
// The object type being validated.
// The property type.
// The type the field schema produces.
// The accessor is a unc{T, TResult}, not an Expression. A compiled lambda
// is a direct call the JIT can inline, costs nothing at start-up, and survives trimming and
// Native AOT untouched. An expression tree would have to be either interpreted on every read or
// compiled through Reflection.Emit, which is exactly what AOT forbids. The property name,
// which is the only thing the expression tree was ever wanted for, is captured at the call site
// by the compiler instead. See docs/decisions/0003-field-access.md.
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

    // Required is read off the child rather than declared here. This library never asks whether a
    // key was present, so the only honest meaning of "required" in a document is that the schema
    // refuses null, and the child is the one that knows.
    internal override PropertyDescription Describe(DescriptionContext context)
    {
        var described = context.Describe(schema);
        return new PropertyDescription(name, described, !described.AllowsNull);
    }

    internal override async ValueTask RunAsync(AsyncParseContext context, T instance)
    {
        context.PushProperty(name);
        await schema.TryParseAsync(context, accessor(instance)).ConfigureAwait(false);
        context.Pop();
    }
}
