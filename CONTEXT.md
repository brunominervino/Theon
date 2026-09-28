# Glossary

The vocabulary of this project. Use these words, in these senses, in code, tests, documentation and
issues. Where two words could mean the same thing, only one of them is ours.

This file is a glossary and nothing else. Design decisions belong in `docs/decisions/`.

## Schema

A reusable, immutable description of what an acceptable value looks like. A schema is built once
and used any number of times, from any number of threads.

A schema is not a validator object that holds state about a particular value, and it is not a
model or a DTO. It describes; it does not contain.

## Theo

The static factory every schema is built from: `Theo.String()`, `Theo.Object<T>()`, `Theo.Int()`.

It is named after the maintainer's son, which is also why the package and namespace are `Theon`.
Not `T`: a type named `T` is shadowed by the type parameter inside any generic that follows the
usual convention, and the call then fails to compile.

## Rule

One condition or normalization within a schema, such as a minimum length or a trim. Rules run in
the order they were written.

We say **rule**, never "check", "validator", "constraint" or "assertion" — all four appear in
neighbouring libraries meaning subtly different things. (The internal type is named `Check`; that
name is not part of the vocabulary and should not leak into public API or documentation.)

## Parse

To run a schema over a value and obtain either the resulting value or the reasons it was rejected.

Parsing covers validation and normalization together. It does **not** cover deserialization: by the
time anything here parses, the value is already a typed .NET object.

## Normalization

A rule that rewrites the value rather than rejecting it, such as trimming or lowercasing. A
normalization affects the rules that follow it, and never the object the value came from.

Distinguish it from a **transformation**, which changes the *type* of the value and can only appear
at the end of a chain.

## Refinement

A rule expressed as a predicate supplied by the caller, rather than one the library provides.

## Field

The binding of one property of an object to the schema that governs it.

We say **field** for the binding and **property** for the C# member it reads. A schema has fields;
a class has properties.

## Error

One reason a value was rejected: a location, a code, and a message.

We say **error**, never "issue", "failure", "violation" or "problem". A single vocabulary matters
here because the type is the library's primary output.

## Error code

The machine-readable identity of an error. Codes are the contract that callers may branch on.

## Message

The human-readable text of an error. A message is never a contract: it is localizable and
replaceable, and no caller should branch on it.

## Path

The location of an error within the value that was parsed, as an ordered list of **segments**. A
segment is either a property name or a collection index.

An error at the root of the parsed value has an **empty path**, not a null one.

## Origin

What kind of quantity a bound was measured against — text, a number, a collection — so that one
error code can carry bounds that need different sentences to describe them.

## Message provider

A caller-supplied function that turns the structured facts of an error into text, or declines and
lets the next provider answer. This is the extension point for localization.
