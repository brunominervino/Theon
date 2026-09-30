# 9. Describe schemas through a reified model, not a switch

Status: Accepted

## Context

A schema knows enough to document itself: its type, its bounds, its formats, its
fields. Turning that into a JSON Schema or an OpenAPI document is the single
largest jump in perceived value this library can make, because it turns
`Theon.AspNetCore` from "validates the request" into "describes the API".

Zod does this in `to-json-schema.ts`, and does it with one large switch over
`def.type`. That is the natural shape for the problem and it is the one thing
this repository refuses in shared code: no branching on a specific schema type.
The rule exists because such a switch turns into a pile of special cases that
only the author of the last one understands, and because every new schema type
then has to be threaded through all of them.

The rule was written to protect the parse path. A document generator is where it
would otherwise be broken first, and breaking it there would be worse than
breaking it in a parse path, because a generator touches every schema type by
construction.

## Alternatives

**A switch over schema types in the generator.** Faithful to the source, and
immediately a list of every schema type in one file, in one place nobody thinks
to update when adding a schema.

**A visitor interface with one method per schema type.** Moves the list into an
interface, so the compiler catches a missing case. It also means every new schema
type is a breaking change to a public interface, and a visitor over eighteen
types is a switch with extra steps.

**Each schema reifies itself.** A schema answers with a `SchemaDescription`: a
data model of kinds, bounds, formats and children. Each rule decorates the
description of the schema that holds it. A writer then turns that model into a
document.

## Decision

Reify. `Schema<TInput, TOutput>` gains an `internal virtual Describe`, `Check<T>`
gains an `internal virtual Describe`, and the only code that switches on what it
is looking at switches on `SchemaDescription`, which is a model this repository
owns and can extend without touching anything that parses.

The description model stays `internal`. The public surface is two things:
`ToJsonSchema`, and `Annotate` for the documentation that rules cannot express.

## Consequences

- Adding a schema type means giving it a description, next to its parse logic,
  in the same file. Nothing central has to be remembered.
- The hook is virtual rather than abstract, so it is not a break for anyone who
  wrote a custom schema against `TryParse`. It is also `internal`, so a custom
  schema from another assembly cannot describe itself and is documented as
  constraining nothing. Making the model public later is additive.
- One dialect: JSON Schema 2020-12, which is what OpenAPI 3.1 uses. OpenAPI 3.0
  predates that alignment, spells nullability with a keyword of its own and has
  no `$defs`; serving it would mean a second writer to keep in step with the
  first.
- A rule with no keyword in the dialect contributes nothing. A refinement is an
  arbitrary predicate, a normalization rewrites rather than constrains, and a
  temporal bound has no keyword that applies to a string. A document that omits
  a rule is incomplete; one that states a rule nothing enforces is wrong, and
  wrong is worse. `Annotate` is the documented way to say such a rule in prose.
- A schema that repeats within a document is written once under `$defs` and
  referred to by `$ref`, which is what makes a recursive schema describable at
  all. Recognising the repeat needs it to be the same instance, so a
  `Theo.Lazy` factory that builds a new schema on every call has nothing to
  recognise; that throws, with a message naming the fix.
- Metadata lives on a wrapper schema rather than in a registry keyed on schema
  instances, which is how Zod does it. A wrapper keeps annotating an ordinary
  immutable builder method and costs one delegating call on a schema that opted
  in; a registry would mean global mutable state with a lifetime to reason
  about, for a feature whose whole job is documentation.

## Trade-offs

There are now two statements of what a rule means: the code that enforces it and
the code that describes it, and they have to agree. Nothing but tests keeps them
in step, and a rule whose description drifts from its behaviour produces a
document that lies. That is the real cost of this decision, and it is paid in
exchange for never writing the switch — which would have the same problem plus a
central list to forget.

Describing allocates freely. A description is produced when a document is
generated, which happens at start-up or in a tool, never on a parse, so the
allocation rules that govern the rest of the library do not apply here and are
deliberately not applied.
