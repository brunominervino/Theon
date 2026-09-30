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

Distinguish it from a **transformation**, which changes the *type* of the value. A transformation
used to have to be the last thing in a chain; it no longer does, and rules may follow it to check
what it produced. See `docs/decisions/0010-transformations-are-not-the-end-of-a-chain.md`.

## Refinement

A rule supplied by the caller, rather than one the library provides.

It takes one of two shapes. A predicate answers yes or no and the library writes the error; a rule
that receives the parse can report for itself, as many times as it has things to say and at whatever
path each one belongs to. See `docs/decisions/0011-a-rule-that-reports-for-itself.md`.

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

## Description

The reified structure of a schema: its kind, its bounds, its formats, its children. A schema
produces one on demand so that a document can be generated without anything having to ask what kind
of schema it is holding.

A description is not a schema and does not validate. It is what a schema says about itself.

## Annotation

Documentation attached to a schema — a title, a sentence, an example, a note that it is deprecated.

An annotation never validates anything. It exists so that a generated document can say what the
rules cannot: why a field exists, what a refinement is checking, which of several acceptable forms
is preferred. We say **annotation** for this and **message** for the text of an error; they are both
prose and they are read by different people at different times.

## Document

A generated JSON Schema, in the 2020-12 dialect. Produced from a description, never from a schema
directly.

## Message provider

A caller-supplied function that turns the structured facts of an error into text, or declines and
lets the next provider answer. This is the extension point for localization.
