# 5. Structured errors, segmented paths, replaceable messages

Status: Accepted

## Context

An error is the library's real output: the success path returns a value the
caller already had. Three things have to be decided together — what identifies
an error, how its location is represented, and where its text comes from.

Message text cannot be the contract. It is displayed to end users, so it must be
translatable and replaceable; anything that branches on it breaks the moment
somebody rewords a string.

Paths are the expensive part. `users[3].address.zipCode` is what a form needs,
but building that string on the way through every nested value would mean paying
for errors that never happen.

## Alternatives for the path

**Build a string as parsing descends.** Simple, and costs an allocation per
level on every parse, valid or not.

**Prefix each error as the stack unwinds**, which is Zod's approach: a child
reports a relative path and each enclosing container prepends its own segment.
Costs nothing when valid, but rewrites each error once per level of nesting.

**Keep a path stack on the parse context and snapshot it when an error occurs.**
Entering a field pushes, leaving pops; the path materializes only at the moment
an error is created, already complete.

## Decision

- Errors carry a `ValidationErrorCode` enum. Codes are the contract; messages
  are not.
- The path is a `ValidationPath` of `PathSegment` values, each either a property
  name or an index, rendered to `users[3].address.zipCode` only on demand.
- The path is snapshotted from a stack held in `ParseContext`, which holds the
  first eight levels in an inline buffer and overflows to an array beyond that.
- Structured facts live on `ValidationErrorInfo` — origin, bound, format,
  inclusivity — separate from the message.
- Messages resolve through a chain: the message attached to the individual rule,
  then the provider on `ParseOptions`, then `SchemaGlobalOptions.MessageProvider`,
  then the built-in English defaults. A provider returning `null` defers to the
  next, so it can localize the cases it cares about and ignore the rest.

## Consequences

- A valid parse allocates nothing at all, including for nested objects. This is
  measured: see `benchmarks/`.
- An error allocates its path array once, at its final length, with no rewriting.
- Callers that need structure — a `ProblemDetails` document, a form-field map —
  read `Segments` rather than taking a string apart again.
- Localization needs no change to the library: supply a provider that switches
  on `Code` and `Origin`.
- Reported bounds are always the declared bound, never the measurement that
  failed it, so `MinLength(5)` given `"ab"` reports 5.

## Trade-offs

`Minimum`, `Maximum` and `Divisor` are typed `object?` and therefore box. That is
one allocation on a path that has already decided to allocate an error, bought in
exchange for preserving the caller's exact numeric type rather than flattening
every bound to `double` and losing `decimal` and `long` precision.

`ParseContext` is a `ref struct`, which rules out an async parse path without a
second design. No asynchronous rule exists yet; when one is needed it will need
its own decision record rather than a quiet widening of this one.
