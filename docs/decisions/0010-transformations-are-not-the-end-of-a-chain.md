# 10. A transformation is no longer the end of a chain

Status: Accepted

Amends: 0002 (two type parameters), and the glossary entry for *transformation*.

## Context

`Transform` took a `Func<TOutput, TNext>` and nothing else, so a transformation
could only be the last thing in a chain. That left the most ordinary pipeline
there is unsayable:

```csharp
Theo.String().Trim().Transform(int.Parse)   // Schema<string, int>
                                            // .Min(1).Max(100)?  Nowhere to put it.
```

Text arrives, becomes a number, and the number has bounds. Every query string,
every form field, every environment variable and every cell in an uploaded file
is that shape, and none of them could be expressed.

There was a second problem underneath it. `int.Parse` throws. A transformation
had to be infallible, so the one conversion people most want — text into a
number — could only be written as a function that throws out of the middle of a
parse. That loses the path, loses every other error found in the same request,
and turns a value a person typed wrongly into an exception, which is the one
thing this library exists not to do.

## Alternatives

**Leave it out and let callers convert before parsing.** Honest, and it means the
conversion failure is reported by something other than the validation library,
in a different shape, at a different time, and not alongside the other errors in
the same request.

**A `Pipe(Schema<TOutput, TNext>)` that chains two schemas.** What Zod does. It is
general, and it is a second way of saying something `Transform` half says
already, so a reader has to learn which of the two a given codebase reaches for.

**Overload `Transform` with the schema that checks its result, and add a `TryTransform`
for the conversion that can fail.** One concept, two shapes of it, and the
failing one shaped like `TryParse` so the framework methods that already have
that shape are handed over as they are.

## Decision

The overload and `TryTransform`.

```csharp
public Schema<TInput, TNext> Transform<TNext>(Func<TOutput, TNext> transform, Schema<TNext> then)

public Schema<TInput, TNext> TryTransform<TNext>(
    TransformAttempt<TOutput, TNext> attempt,
    string message,
    Schema<TNext>? then = null)
```

`TransformAttempt<TFrom, TTo>` is a delegate of this library's own, declared
`bool (TFrom value, out TTo result)`, which is exactly `int.TryParse`,
`Guid.TryParse` and `DateTimeOffset.TryParse`. A caller writes
`TryTransform<int>(int.TryParse, "Must be a whole number.")` and nothing else.

## Consequences

- The glossary sentence saying a transformation "can only appear at the end of a
  chain" is no longer true and has been changed. It was a statement about the
  implementation, not about the idea.
- A failure after the conversion reports at the path the value came from, so a
  page size out of range is reported against `PageSize` and not against the
  number it became. That is the whole reason to put this in the library rather
  than at the call site.
- The conversion runs only after the schema before it passed, so it never sees a
  value already known to be unacceptable — the same order every other rule in
  this library runs in.
- A generated document cannot express any of it. A document says what the caller
  sends, and the dialect has no way to state a bound on what this program made of
  that. Both of these record that they could not be expressed, which
  `UnrepresentablePolicy.Throw` reports.
- Nothing on the synchronous path allocates that did not allocate before: the
  follow-on schema is run on the same context, not on one of its own.

## Trade-offs

`Transform` now has two overloads and a sibling, where it had one method. That is
the cost of the pipeline being expressible at all, and the alternative — a second
verb that partly overlaps the first — costs a reader more.

`TryTransform` takes a message, which means the conversion failure is reported
with one sentence for every way the conversion could have failed. That is right
for "this is not a whole number" and would be wrong for a conversion with several
distinct failure modes. Such a conversion wants a schema of its own, or the
contextual `Refine` of decision 11.
