# 2. Two type parameters, with a one-parameter shorthand

Status: Accepted

## Context

Most schemas neither widen nor narrow a type: a rule for a `string` accepts a
`string` and yields a `string`. But `Transform` genuinely changes the output
type, and a schema that turns a string into an integer should be typed as such
rather than erased to `object`.

TypeScript can carry an input and an output type through inference without the
reader ever writing either one. C# cannot: it has no partial generic inference,
so every type argument a signature declares appears in full in IntelliSense, in
compiler errors, and in any variable a caller declares. A two-parameter schema
therefore costs ergonomics on every single use, not just the ones that need it.

## Alternatives

**`Schema<T>` only.** Simplest to read. `Transform` would either be impossible
or would have to erase its result.

**`Schema<TInput, TOutput>` only.** Honest, but `Schema<string, string>` appears
everywhere, including in the 95% of cases where the two are the same.

**Both, with inheritance.** `Schema<T> : Schema<T, T>`.

## Decision

Both: `Schema<TInput, TOutput>` is the real abstraction, and
`Schema<T> : Schema<T, T>` is the shorthand almost everything uses.

## Consequences

- Everyday code reads `Schema<string>` and `Schema<CreateUserRequest>`.
- `Transform` returns `Schema<TInput, TNext>`, so the type reflects the change.
- A `Schema<T>` is usable anywhere a `Schema<T, T>` is expected, because it is
  one, so `ObjectSchema.Field` accepts either without an overload.
- `Field` is declared over `Schema<TValue, TParsed>`, and inference resolves
  both parameters from a `Schema<T>` argument through the base class.

## Trade-offs

Two public types instead of one, and a reader who goes looking for the base
implementation finds it one level up from where they started. That is a small,
one-time cost against a verbosity tax that would otherwise be paid on every
declaration in every consuming codebase.
