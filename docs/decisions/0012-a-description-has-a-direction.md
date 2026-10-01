# 12. A description has a direction

Status: Accepted

Amends: 0009 (describe through a reified model), and the glossary entry for *description*.

## Context

A schema that transforms has two shapes. `Theo.String().Trim().TryTransform<int>(int.TryParse,
"Must be a whole number.", Theo.Int().Min(1).Max(100))` accepts text and produces a number between
one and a hundred, and those are two different documents.

The generator only ever wrote the first one. `TransformSchema.Describe` returned the description of
the schema it wrapped, with a comment saying a document describes what a caller sends. That is right
for a request body, a query string, a configuration file — anywhere the document exists to tell
somebody else what to send.

It is wrong for a response body, which is the other half of every API this library is used to
describe. A response document should say what the caller will receive, and what the caller will
receive is the output side. Asking for one and getting the other is not a partial answer; it is the
wrong answer, and nothing in the library would have noticed.

The same split runs through the wrappers. `Default` accepts null and never produces it. `Catch`
accepts anything and produces either what the inner schema accepted or the fallback. Each of those
had one description where it needed two.

## Alternatives

**Two methods.** `ToJsonSchema` and `ToResponseJsonSchema`, or `Describe` and `DescribeOutput`. Two
entry points that differ by a word, each needing its own overload for the document form, and a
`DescriptionContext` that would have to be told which one it was serving anyway.

**A parameter on `Describe`.** `Describe(DescriptionContext context, DescriptionDirection direction)`.
Honest at the signature, and every composite schema would then have to remember to pass it on to its
children. One that forgot would describe the outside of an object and the inside of nothing, which is
a silent, plausible-looking wrong answer — the exact failure mode this library spends effort avoiding.

**An option, carried on the context.** One value on `JsonSchemaOptions`, read once, placed on the
`DescriptionContext` that already walks the tree. A composite schema that describes its children
through the context propagates the direction without knowing that it exists.

## Decision

The option, carried on the context. `DescriptionDirection` is `Input` or `Output`, and
`JsonSchemaOptions.Direction` defaults to `Input`.

`Input` is the default because it is what every document generated before this option existed said.
Zod's equivalent defaults to the output side; adopting that here would quietly rewrite the OpenAPI
description of every application that already uses this library, to say something different about the
same endpoints.

The enum lives in `Theon` rather than `Theon.Metadata`, next to `UnrepresentablePolicy`. Both are
options on the same type, and a caller setting one should not need a second `using` for a namespace
whose other contents they have no business touching.

Six wrappers describe the two sides differently:

- `TransformSchema` and `TryTransformSchema` — the input is the inner schema; the output is the
  follow-on schema, when there is one.
- `DefaultValueSchema` and `DefaultReferenceSchema` — the input accepts null and names what it
  becomes; the output is exactly what the inner schema produces.
- `CatchSchema` — the input is the shape worth aiming for; the output is that shape *or* the fallback.

Two look as though they should differ and do not, which is worth as much as the six that do:

- `RequiredValueSchema` and `RequiredReferenceSchema` are declared `Schema<T?>`, so the reflex is
  that the output may be null. It may not. The only way a null leaves one of these is a parse that
  failed, and a description describes the values a parse succeeds with.
- `NullableValueSchema` and `NullableReferenceSchema` hand a null straight back, so null belongs in
  both sides.

Every one of the eight is fixed by a test in `DescriptionDirectionTests` that asserts both
directions, because reasoning about which side a wrapper is on is exactly the kind of thing that is
convincing and wrong.

## Consequences

- The output side of a transformation *with* a follow-on schema is fully describable, where the input
  side never is. Decision 10 recorded that a document "cannot express any of it"; that was a
  statement about the input side, and it is no longer the whole truth. `OnUnrepresentable.Throw` now
  passes on the output side of such a schema, and that is correct rather than a hole.
- A transformation *without* a follow-on schema has a type and no schema, so the output description
  is the JSON kind that type travels as — `integer` for a `Transform(text => text.Length)` — and
  nothing more. This is deliberately not silence: the type is a fact this library owns, and writing
  it down is incomplete where omitting it tells the reader nothing and inventing a constraint would
  be wrong. It is still recorded as unrepresentable, because a response document that says only "an
  integer" is not a contract, and that is the argument for the overload that takes a schema.
- A transformation to a type with no JSON shape at all describes nothing, which is the same answer
  the base description gives for a schema this assembly does not know.
- `CatchSchema` is the one place the output side is *more* precise than the input side. The output is
  written as `anyOf: [the inner shape, const: the fallback]`, because
  `Theo.String().Email().Catch("none")` genuinely does produce `"none"`, and a response document
  claiming to produce only e-mail addresses would be wrong rather than incomplete.
- `Default` writes no `default` keyword on the output side. `default` is an annotation about what
  happens to an absent input, and there is no absent value on the way out.
- A normalization is not modelled. `Trim` narrows what comes out without changing its type, and the
  dialect has no way to say "this has no leading space", so both sides of a normalized value are
  described identically. A reader who needs that said should say it with `Annotate`.

## Trade-offs

Four public members for something most schemas ignore. A schema that neither transforms, defaults nor
catches produces the same description either way, so the majority of callers will set this option
never. The price is paid by everyone and collected by the people documenting a response, which is
half of every API and the half this generator could not serve at all.

Carrying the direction on the context rather than in the signature means a reader of
`Describe(DescriptionContext)` cannot see that a direction exists. That is the cost of propagation
being automatic, and it is the right way round: a signature that advertises the parameter and relies
on thirty implementations to forward it will have one that does not.
