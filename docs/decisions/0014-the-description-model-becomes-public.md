# 14. The description model becomes public

Status: Accepted

Amends: 0009 (describe through a reified model).

## Context

Decision 9 built a reified description model so that a document could be generated without anything
switching on schema identity, and it kept that model `internal` on purpose. The reason was good: an
internal model can be changed freely, and the public surface was two things — `ToJsonSchema` and
`Annotate`. It also said, in as many words, that making the model public later would be additive.

Two consequences had been accumulating.

**A custom schema cannot describe itself.** `TryParse` is public and documented as the extension
point, so a schema written in another assembly parses as well as any built-in one. `Describe` was
`internal virtual`, so the same schema says nothing about itself, and a document generated for one
constrains nothing. That is not a corner: it is every custom schema anyone writes, and it is also
what stopped `Theon.AspNetCore` — a different assembly — from describing a schema over a type the
core cannot know.

**Nobody can write a generator.** The model is a complete, format-neutral account of what a schema
requires, and the only thing allowed to read it was one JSON Schema writer. A generator for protobuf,
for Avro, for rendering a form, for a documentation page — all of them need exactly what
`ToJsonSchema` needs, and all of them were impossible.

The escape hatch that would have avoided this decision was `InternalsVisibleTo` for
`Theon.AspNetCore`. That is worse than doing nothing: it makes the model a contract in fact for the
one consumer that matters most, without making it a contract in law, and the first time the model
changed under it the breakage would be invisible to every tool that checks for breakage.

## Decision

Make it public, and make it immutable while doing so.

Public: `SchemaKind`, `SchemaDescription`, `PropertyDescription`, `DescriptionContext`,
`SchemaDescriptionSet`, `Schema<TInput, TOutput>.Describe`, and a `Describe` extension that produces a
set for a generator to read.

Internal, still: `Check<T>` and its `Describe`, `Representable`, `SchemaKinds`, `JsonSchemaWriter`, and
a new `SchemaDescriptionBuilder`.

### `SchemaDescription` is immutable, with a copy constructor

It was a mutable class with twenty-seven settable properties, built up in place. That is defensible
for an internal type — decision 9 defended it, and the defence still holds: a description is produced
once when a document is generated, and nothing is shared. It is a poor public API, because a caller
holding one cannot tell whether it is theirs to change, and because nothing stops a length bound being
put on an integer.

Properties are `init`, collections are `IReadOnlyList<>`, and there is a copy constructor. A wrapper
schema reads:

```csharp
public override SchemaDescription Describe(DescriptionContext context) =>
    new(context.Describe(inner)) { AllowsNull = true };
```

which is shorter than the three mutating lines it replaces.

**Not a `record`**, which was the obvious reach. A record's structural equality would compare the
collection members by reference, so two descriptions of the same thing would come back unequal — an
equality that is worse than no equality, because a caller would reasonably expect it to work.

**Not a public builder.** A builder is genuinely needed: twenty-one rules each decorate the
description of the schema that holds them, and they need somewhere mutable to write. But they are
`Check`s, and `Check` is internal — a schema from another assembly has none of ours and writes its
description with an object initializer instead. So the builder stays internal, and the public surface
pays nothing for it. That is the whole reason this shape is affordable.

A test sets every property by reflection, copies, and compares. A property the copy constructor forgets
is a constraint that disappears from every document containing a nullable, defaulted or annotated
value, and a hand-written list of properties to check is exactly the thing that would be forgotten
alongside it.

### `DescriptionContext` promises two things

`Describe(schema)` and `Direction`. Nothing else.

`Describe` has to be the contract: it is what recognises a schema that contains itself, and a composite
schema that called its children's `Describe` directly would run until the stack ended on any recursive
schema. `Direction` has to be, because a custom schema that transforms needs to know which side it is
being asked about.

Everything else is detail and stays internal. `Definitions` in particular: exposing it would fix the
definitions as a dictionary keyed by string for ever, and a schema describing its children has no use
for it.

The constructor is internal. A caller who wants to drive the walk calls `schema.Describe(...)` and gets
a `SchemaDescriptionSet` back, so we commit to the result rather than to how a context is built.

## Consequences

- `Theon.AspNetCore`, and any other assembly, can now ship a schema that describes itself. This is what
  makes a schema over a type the core cannot know describable at all.
- `JsonSchemaNode` gained `Description`, so an amendment can read the structured facts as well as the
  rendering — which is how it recovers a temporal bound the dialect had nowhere to put. Additive, and
  only possible once the model was public.
- The public surface went from 54 types and 313 members to 60 and 380. That is the largest single
  increase this library has taken, and it is mostly `SchemaDescription`'s own properties: it is a data
  model, and a data model is mostly properties.
- `SchemaKind` carries a `CA1720` suppression. Its members are the JSON type names verbatim — `String`,
  `Integer`, `Object` — which is what a reader of a generated document sees and what the dialect's own
  `type` keyword takes. Renaming them to avoid a CLR type name would mean the vocabulary no longer
  matched the thing it describes.
- Describing still allocates freely, and still never happens on a parse.

## Trade-offs

This is permanent. Decision 9's reason for staying internal was that the model could then be changed
freely, and that freedom is now spent. A keyword the dialect gains, or a kind of constraint this
library learns to express, has to arrive as an additional property rather than as a reshaping — and a
reshaping is sometimes what the right answer looks like.

The judgement is that the model has been stable across every release so far, that the two things it
blocks are both things people actually want to do, and that the alternative on offer
(`InternalsVisibleTo`) was the same commitment with none of the protection. `EnablePackageValidation`
now watches it, which is more than was true of the internal version.

`SchemaKind` being a closed enumeration is the one place where the freedom is genuinely lost rather
than merely constrained. A custom schema cannot invent a kind. That is tolerable because there are
only so many kinds of JSON value and the enumeration already has all of them; a format with a kind
JSON does not have is a format this model was never going to describe anyway.
