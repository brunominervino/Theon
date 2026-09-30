# 8. Asynchronous validation as a second path, not a replacement

Status: Accepted

## Context

Some rules cannot be answered from the value alone. Whether an address is already registered,
whether a code exists, whether a document appears on a list — each needs a round trip, and a round
trip needs awaiting. These are among the most commonly requested validations in real applications.

They were not merely missing; they were impossible. `ParseContext` is a `ref struct`, which is
what lets a valid value parse without allocating, and a `ref struct` cannot cross an `await`. That
is a language rule, not an oversight.

The cost of deciding grew with every release. `TryParse` takes `ref ParseContext` and is public —
it is the extension point for custom schemas — so changing it is a break for every consumer who
wrote one.

## Alternatives

**Declare asynchronous validation out of scope.** Honest, and cheap. It also means the library
cannot express a rule that a large share of applications need, and those applications end up doing
that validation somewhere else, which defeats having one place where the rules live.

**Make `ParseContext` a class.** One context, one code path, asynchrony throughout. It also makes
every parse allocate, including the synchronous ones, which gives up the property this library is
built around. Pooling could hide the allocation, at the cost of reentrancy hazards that are
especially unpleasant in asynchronous code.

**A second, parallel path.** Keep the synchronous context exactly as it is. Add an
`AsyncParseContext` that is a class, and a `TryParseAsync` whose default implementation runs the
synchronous one.

## Decision

The parallel path.

`TryParseAsync` is virtual on the base schema, and its default implementation borrows a synchronous
context seeded with the path walked so far, runs the synchronous parse, and moves any errors it
raised into the asynchronous context. Every schema without an asynchronous rule therefore works on
the asynchronous path with no code of its own.

Composite schemas — object, collection, and the nullable, required and transform wrappers —
override it, so asynchrony reaches their children. A schema that genuinely awaits something
overrides it too.

A schema containing an asynchronous rule throws `SchemaAsyncUsageException` if parsed
synchronously.

## Consequences

- The synchronous path is untouched, and still allocates nothing for a valid value.
- An asynchronous parse allocates one context, which is noise beside the I/O that made it
  asynchronous.
- `RefineAsync` is an extension on `Schema<T>`, so it composes onto any schema without each schema
  type needing to know about it.
- An asynchronous rule runs only after everything before it has passed, so a malformed address
  never costs a database round trip.
- `CancellationToken` reaches the rules, and composite schemas check it between children.
- Fields and elements are awaited in order, not in parallel. Running them together would turn one
  slow lookup per request into several concurrent ones, and ordered errors are something a caller
  can match back to the input.

## Trade-offs

Two paths mean two places where a composite schema's traversal is written, and they have to agree.
The tests check that they do, by asserting the same outcomes through both. The alternative —
one path at the cost of allocating on every synchronous parse — trades a property every caller
benefits from for a symmetry only the maintainers see.

Refusing to block on the synchronous path will surprise someone. It is deliberate: blocking on I/O
inside a request handler is how thread-pool starvation begins, and a loud exception at the first
call is kinder than a deadlock under load.
