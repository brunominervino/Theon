# 1. Validate materialized values, do not deserialize

Status: Accepted

## Context

Theon takes its conceptual lead from Zod. Zod's central operation is
`unknown -> T`: it exists because TypeScript erases types at run time, so
crossing the boundary from untrusted JSON to a typed value requires a runtime
witness, and Zod is that witness. Roughly half of Zod's surface follows from
this — `z.object()` checking that keys exist, `z.string()` checking that a value
is a string, unknown-key policies, and much of its type-level machinery.

.NET does not have that problem. `System.Text.Json`, with its source generator,
already performs `JSON -> T` quickly, AOT-safely, and is maintained by the
platform vendor. By the time application code holds a `CreateUserRequest`, the
shape question has been settled by the runtime.

## Alternatives

**Reimplement parsing from untyped input.** Accept `JsonElement`,
`IDictionary<string, object?>`, or raw text and produce `T`. This is the most
faithful translation of Zod, and it would let one schema describe both shape and
values.

**Validate values on an already-materialized object.** Let `System.Text.Json` do
what it is good at, and confine this library to whether the values inside the
object are acceptable.

## Decision

Validate materialized values. A schema's input is a typed value; it is never a
`JsonElement` or a dictionary.

## Consequences

- No unknown-key policy, no missing-property handling, no shape checking. The
  type system settled all three before parsing started.
- No reflection is needed to discover an object's shape, which is most of why
  Native AOT support is achievable at all.
- The library cannot report "this JSON was malformed" — that error belongs to
  the deserializer and arrives before any schema runs.
- `Coerce` in Zod's sense has no place here. Converting `"42"` into `42` is
  expressed as a `Schema<string, int>` through `Transform`, where the types say
  what happened.

## Trade-offs

We give up the single-schema story where one definition both parses and
validates. In exchange we avoid competing with a faster, better-funded
deserializer, and we avoid the reflection that competing would require. The
honest framing of what remains is that this is a validation library with a
parsing-shaped pipeline, not a parser.
