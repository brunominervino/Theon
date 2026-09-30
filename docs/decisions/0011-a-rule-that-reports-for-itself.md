# 11. A refinement may report for itself

Status: Accepted

Amends: the glossary entry for *refinement*.

## Context

`Refine` took a predicate and a message. It answers yes or no, and a failure
becomes one error, with the `Custom` code, at the path the value is at. That is
the right shape for most rules and it cannot express three things people
repeatedly want:

- A rule with more than one thing to say. Two passwords that do not match *and* a
  password that contains the address are two complaints, and the caller has to
  pick one or write two rules that each recompute the same state.
- A rule whose failure belongs against a particular property rather than against
  the object it was checked on. `When` has an overload for this; an arbitrary
  predicate did not.
- A rule that wants a code a caller can branch on. Everything a refinement
  produces is `Custom`, and codes are the contract, so a caller who needs to know
  *which* refinement failed has to match on message text — which the error model
  explicitly says is not a contract.

The escape hatch was to implement a schema against the public `TryParse`. That
works and is a great deal of ceremony for one rule, so in practice nobody does it
and the rule gets written as something weaker instead.

## Alternatives

**Leave it.** `TryParse` is public and documented as the extension point, so
nothing is impossible. It is merely out of proportion.

**Let the predicate return a list of errors.** No new delegate shape, and it
allocates a list on a path that has not decided to fail yet, which is the one
thing the design of this library will not do.

**Give the rule the parse context.** The context already has `AddError`,
`PushProperty` and `Pop`, all public, all documented. A rule that receives it can
do everything a schema could, and nothing has to be invented.

## Decision

The context, through a delegate of this library's own:

```csharp
public delegate void RefineRule<in T>(T value, ref ParseContext context);

public static Schema<T> Refine<T>(this Schema<T> schema, RefineRule<T> rule)
```

An overload of `Refine` rather than a new verb, because it is the same idea — a
rule the caller supplied — in a different shape. A rule that reports nothing has
accepted the value.

## Consequences

- The glossary said a refinement is "a rule expressed as a predicate supplied by
  the caller". It is now a rule supplied by the caller, which may be a predicate
  or may report for itself.
- The delegate is declared here rather than being an `Action<T, ParseContext>`,
  because `ParseContext` is a `ref struct` and cannot be a type argument on
  `net8.0`. C# 13's `allows ref struct` would express it and is not available on
  both target frameworks, so a delegate with a `ref` parameter is the portable
  answer. This was verified to compile on both before the shape was chosen.
- It is an extension method on `Schema<T>`, next to `RefineAsync`, so it composes
  onto any schema without each schema type needing to know about it.
- The rule runs only once the inner schema has passed, which is the order
  `RefineAsync` already uses: a rule never sees a value already known to be
  unacceptable.
- On the asynchronous path the rule is handed a synchronous context positioned
  where the parse has reached, so a path it pushes still resolves correctly.
- A generated document cannot express an arbitrary rule, and records that it
  could not.

## Trade-offs

This hands a caller the parse context, which means a caller can now write a rule
that reports at a path unrelated to the value it was given, or that reports
nothing when it should report something. That was already true of anyone
implementing `TryParse`; what changes is how easy it is to reach. The judgement is
that a library whose extension point is out of proportion to the task gets
extended badly or not at all, and that is worse than one that can be misused.

Two shapes of `Refine` also mean a reader has to notice which one is in front of
them. The signatures differ by more than a subtlety — one takes a message and one
does not — and the predicate form remains the one to reach for by default.
