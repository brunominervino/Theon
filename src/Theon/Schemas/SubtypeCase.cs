using Theon.Metadata;

namespace Theon.Schemas;

// One branch of a discriminated union: a subtype, and the schema that governs values of it.
//
// The non-generic base exists so that one schema can hold branches for many different subtypes in a
// single array, the same trick FieldBinding uses. The derived type is erased here and nowhere else.
internal abstract class SubtypeCase<TBase>
    where TBase : class
{
    internal abstract Type Subtype { get; }

    internal abstract bool Matches(TBase value);

    internal abstract void Run(ref ParseContext context, TBase value);

    internal abstract ValueTask RunAsync(AsyncParseContext context, TBase value);

    internal abstract SchemaDescription Describe(DescriptionContext context);
}

// A branch for one subtype.
//
// Matching is a type test, not a lookup keyed on a property. That is the whole difference from the
// TypeScript design: there, a union member has to carry a literal field because the runtime has
// erased the type, so the discriminator is data. Here the discriminator is the type, the compiler
// checked that TDerived really is a TBase, and System.Text.Json already resolved which subtype
// arrived before any schema ran. A cast after a successful test costs nothing and needs no
// reflection.
internal sealed class SubtypeCase<TBase, TDerived>(Schema<TDerived, TDerived> schema)
    : SubtypeCase<TBase>
    where TBase : class
    where TDerived : class, TBase
{
    internal override Type Subtype => typeof(TDerived);

    internal override bool Matches(TBase value) => value is TDerived;

    internal override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Describe(schema);
    }

    internal override void Run(ref ParseContext context, TBase value) =>
        schema.TryParse(ref context, (TDerived)value, out _);

    internal override async ValueTask RunAsync(AsyncParseContext context, TBase value) =>
        await schema.TryParseAsync(context, (TDerived)value).ConfigureAwait(false);
}
