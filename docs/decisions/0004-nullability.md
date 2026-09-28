# 4. One nullability concept, not two

Status: Accepted

## Context

Zod distinguishes `.optional()` from `.nullable()` because JavaScript has two
distinct absent values. `undefined` means the key was not there; `null` means it
was there and held nothing. Zod needs both, and it needs a third rung
(`defaulted`) to describe how the two interact with defaults.

C# has one absent value. A missing JSON property does not produce a distinct
"undefined" — it leaves the property at its default, which for a reference type
or a `Nullable<T>` is `null`. There is no expressible difference between "absent"
and "null" on a materialized object.

## Alternatives

**Mirror Zod with `Optional()` and `Nullable()`.** Faithful to the source, and
familiar to anyone arriving from Zod.

**A single concept.** One method, named for what it actually does.

## Decision

One concept, spelled `AllowNull()`. There is no `Optional()`.

`AllowNull()` returns `Schema<T?>`. For a reference type that is the same type
at run time with a different annotation; for a value type it is a genuine
`Nullable<T>`, which is why there are two internal wrappers.

## Consequences

- No three-rung optionality ladder, and none of the interaction rules between
  optionality and defaults that follow from having one.
- `Schema<T?>` composes into `Field` like any other schema.
- Nullable reference type annotations flow through, so the compiler tells a
  caller when a value can be null.

## Trade-offs

Someone arriving from Zod will look for `Optional()` and not find it. That is
the correct outcome: offering it would mean either a synonym for `AllowNull`,
which invites the reader to hunt for a difference that does not exist, or
inventing a distinction the runtime cannot represent.
