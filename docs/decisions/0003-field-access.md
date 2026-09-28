# 3. Field access by delegate, with the name taken from the call site

Status: Accepted

## Context

An object schema needs two things from each field: a way to read the value, and
the property's name for error paths. The reflex in .NET is
`Expression<Func<T, TValue>>`, because one expression tree supplies both.

That reflex is expensive. An expression tree allocates a small object graph at
construction; reading the value then requires either `Compile()`, which goes
through `Reflection.Emit` and is unavailable under Native AOT, or the expression
interpreter, which is roughly an order of magnitude slower than a direct call.
Analysing the tree to recover the member name costs more allocation again. All
of that to learn something the compiler already knew.

## Alternatives

**`Expression<Func<T, TValue>>`.** Familiar; the standard approach in
FluentValidation and in most .NET validation libraries. Costs start-up time,
allocation, and AOT compatibility.

**String property names, with reflection to read the value.** Loses compile-time
safety and refactoring support; also needs reflection.

**A source generator over the model type.** Fastest and fully AOT-safe, but it
is a whole build-time component to own before the runtime model has been proven,
and it forces every consumer into a generator they may not want.

**A plain `Func<T, TValue>`, with the name captured by
`CallerArgumentExpression`.** The delegate is a direct call. The compiler hands
over the literal source text the caller wrote, from which the name is read once,
while the schema is being built.

## Decision

Use `Func<T, TValue>` for reading and `[CallerArgumentExpression]` for the name.

```csharp
public ObjectSchema<T> Field<TValue, TParsed>(
    Func<T, TValue> accessor,
    Schema<TValue, TParsed> schema,
    [CallerArgumentExpression(nameof(accessor))] string? accessorExpression = null)
```

For `x => x.Email` the name is whatever follows the last dot.

## Consequences

- No reflection, no `Reflection.Emit`, no expression interpreter. Native AOT and
  trimming work with no annotations and no warnings.
- Field access at parse time is an ordinary delegate call the JIT can inline.
  This is the main reason a valid nested object parses with zero allocations.
- The name is recovered once per schema, at start-up, and never during a parse.
- An accessor that is not a plain property read throws at construction, naming
  the overload that takes an explicit name. Failing at the line that caused it
  beats surfacing a wrong path in an error message weeks later.
- `Refine` reuses the same mechanism to attach a cross-field error to a named
  property.

## Trade-offs

The name is recovered from source text rather than from metadata, so it reflects
what was written rather than what it resolved to. A property renamed through an
IDE updates the source text too, so the two stay in step; a caller who writes
something more elaborate than a property read gets an exception instead of a
guess. Both failure modes are loud and immediate, which is what we want.
