# 16. Reading a document back into a schema

Status: Accepted

Amends: 0001 (validate materialized values), by drawing the line it left implicit.

## Context

The library generates a JSON Schema from a schema. The other direction — a schema from a document —
serves a case the first one does not: checking a payload against a description somebody else
published. A third party's OpenAPI description, a contract test, a configuration file whose shape is
declared elsewhere. There is no C# type to hand `Theo.Object<T>()` in any of those, because the type
belongs to whoever wrote the document.

Decision 1 says this library validates materialized, typed values and does not deserialize. A JSON
Schema describes JSON, which has no types in the C# sense, so "document to `Schema<T>`" would mean
discovering `T` and matching property names to it by reflection — the thing decision 1 exists to make
unnecessary and the thing `IsAotCompatible` turns into a build error.

## Why this is not the coercion decision 1 refused

Decision 1 refused `unknown -> T`. It refused it because `System.Text.Json` already does that job
faster, AOT-safely, and with the platform vendor maintaining it, and because competing would require
reflection.

`JsonNode` is a materialized value. It has been through `System.Text.Json` already; the deserializer
has done its job and handed back an object graph. Validating one is the same operation this library
performs on a `CreateUserRequest`: the value exists, it is typed, and the question is whether what is
inside it is acceptable.

What this deliberately does **not** do is produce a typed object. `Theo.JsonSchema(document)` is a
`Schema<JsonNode?>` and stays one. A caller who wants `CreateUserRequest` deserializes into it and
uses `Theo.Object<CreateUserRequest>()`, which is the same division of labour decision 1 drew.

The distinction worth keeping in mind is between *untyped input* and *deserialization*. This is the
first schema here whose input has no particular shape. It is not the first — and still is not any —
that turns text into an object.

## Decision

`Theo.JsonSchema(JsonNode document)` returns a `Schema<JsonNode?>`.

**The description model is the pivot.** A document is read into a `SchemaDescription` tree, and one
schema walks that tree over a `JsonNode`. The alternative was a representation of its own, which
would have been a second model of "what an acceptable value looks like" to keep in step with the
writer. Using the one that exists also makes the cycle closeable: a document generated from a schema,
read back, and generated again is the same document, and that is asserted for every shape this
library writes.

This is also the real reason this came after decision 14. Not because the model had to be public —
this lives in the same assembly — but because the model had to be settled before it could carry
traffic in both directions.

**A keyword this cannot honour throws, when the schema is built.** `oneOf`, `not`, `if`, `then`,
`else`, `patternProperties`, `propertyNames`, `prefixItems`, `contains`, `dependentSchemas`,
`dependentRequired`, `unevaluated*`, `contentSchema`, `$dynamicRef`. Leaving an assertion out would
make the schema *more permissive* than the document, which is the direction that lets a bad value
through while the caller believes it was checked. Throwing at construction is the same bargain the
rest of the library makes: fail at the line that caused it.

`oneOf` is the one that is genuinely wanted and is still refused. It means *exactly* one branch, where
`Theo.OneOf` means *at least* one, and the description model has `anyOf` and no way to say the
difference. Mapping one to the other would be wrong in the permissive direction. Supporting it later
is additive.

**Both keywords of a bound pair together are refused.** `minimum` and `exclusiveMinimum` are
independent assertions in 2020-12 and may legally appear on the same schema, where a description
carries one bound. Keeping one and dropping the other reads `{"minimum": 5, "exclusiveMinimum": 10}`
as "greater than 5" and lets 7 through, which the document rejects. The same for the maximum pair and
for `minItems` beside `minProperties`. Nothing this library writes carries both, so the round trip
never meets it.

**`"type": "null"` restricts the value to null.** The description model had no kind for it and the
reader collapsed it into `Unknown`, which is the kind that means "nothing was said" — so a schema
accepting only null accepted everything. `SchemaKind` gained `Null`: it is the one JSON type the
enumeration was missing, nothing here produces a schema of that kind, and the reading side needs it to
be able to say what a document said.

**`required` is honoured for names `properties` does not mention.** The two keywords are independent:
a document may demand a key without saying anything about its value, and `{"required": ["id"]}` with
no `properties` at all is a legal schema that rejects `{}`. Reading only the names both keywords
mention dropped the demand. Those names become properties with an empty description, which constrains
nothing and is present — which is exactly what the document says.

**A keyword that asserts nothing is ignored**, which is what the dialect requires: `$schema`, `$id`,
`title`, `description`, `examples`, `default`, `deprecated`, and anything unrecognised, including the
`x-` extensions that every real OpenAPI document is full of.

**An unknown `format` is ignored**, which is the one place something is let through, and it is let
through on purpose. 2020-12 makes `format` an annotation unless the format-assertion vocabulary is in
use, so ignoring one is what a validator is supposed to do — and refusing would make this useless on
real documents, which carry `int64`, `password` and a hundred names nobody has written a rule for. A
format this library *does* have is enforced with that rule and not a second implementation of it: a
reading side with its own e-mail check would drift from the writing side and stop accepting its own
documents.

**A `pattern` is compiled non-backtracking, or refused.** A pattern out of somebody else's document is
text from a stranger, which is the case `AGENTS.md` requires linear-time matching for. It cannot be a
`[GeneratedRegex]`, because it is not known at compile time, so it is `RegexOptions.NonBacktracking` at
construction — and where the engine will not build one that way, which lookaround and backreferences
both cause, the pattern is refused rather than matched by a backtracking engine.

**References resolve by JSON pointer from the document root**, which makes `#/$defs/Comment` and
`#/components/schemas/Comment` the same mechanism rather than two cases. A pointer outside the
document throws: nothing here fetches one, and silently not following a reference would accept
anything in its place.

## Consequences

- One public member, `Theo.JsonSchema`. The reader, the validator and the format table are all
  internal, and what comes back is an ordinary `Schema<JsonNode?>` with every method a schema has.
- A document that contains itself works, and a value nested beyond `ParseOptions.MaxDepth` is reported
  rather than ending the stack. The walk bounds the *schema* as well as the value: a reference that
  comes back to itself through `anyOf` or `allOf` advances nothing about the value, so a guard counting
  only how deeply the value is nested never fires on it, and the result is a stack overflow — which
  cannot be caught — from a legal document somebody else published. The references followed are counted
  against the same limit. The definitions become separate schema instances sharing one map,
  which is what lets the factory build a recursive schema without recursing while it does so.
- Three of these came out of review rather than out of writing, and all three were in the same
  direction: a schema quieter than the document it was read from. That is the direction worth
  reviewing for, and it is why the rest of the reader throws rather than guesses.
- A missing property is a real failure here, which it is nowhere else in this library — this is the
  one schema whose input has not been through a type first, so `required` means something.
- A recursive document round-trips structurally but not by name: the definition comes back named after
  the CLR type the schema produces, `JsonNode`, rather than after whatever the original called it. The
  generator names a definition from the type it validates, and every definition here validates the
  same type. Fixed by a test so that nobody finds it by accident.
- The assembly ceiling went from 160 KiB to 192 KiB. The feature took it to within five hundred bytes
  of the old one, which is passing by luck rather than by design.

## Trade-offs

A schema over a `JsonNode` is weaker than a schema over a type, and somebody will reach for it when
they should have written a type. The documentation says which job it is for in its first sentence, and
the return type says the rest: nothing typed comes out of it.

Two directions mean two places that have to agree about what a keyword means, and they are checked
against each other rather than asserted — the round-trip test is the whole reason to trust either. A
keyword this library writes and does not read, or reads differently, fails that test.

The supported subset is smaller than the dialect. That is deliberate and visible: the exception names
every keyword it does support, so a document that cannot be read says so at the line that read it
rather than by quietly accepting a value it should not have.
