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

The static factory every schema the core can know about is built from: `Theo.String()`,
`Theo.Object<T>()`, `Theo.Int()`. See **Upload** for the one exception and why it has to exist.

It is named after the maintainer's son, which is also why the package and namespace are `Theon`.
Not `T`: a type named `T` is shadowed by the type parameter inside any generic that follows the
usual convention, and the call then fails to compile.

## Upload

A file arriving in a multipart form request, and the subject of the second factory.

`Upload.File()` and `Upload.Files()` live in `Theon.AspNetCore`, because `IFormFile` is an ASP.NET Core
type and the core package has no dependencies. So **`Theo` is the factory for every schema the core can
know about**, and `Upload` is the factory for the one it cannot. We say **upload** for the thing and
`Upload` for the factory; never `FormFile`, which is already a type in `Microsoft.AspNetCore.Http` and
would collide. See `docs/decisions/0015-a-second-factory-for-uploaded-files.md`.

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

The model is public, and immutable. A schema produces one by overriding `Describe`, and anything that
reads one — the JSON Schema writer, or a generator somebody else writes — reads a data model this
project owns rather than asking what kind of schema it is holding. See
`docs/decisions/0014-the-description-model-becomes-public.md`.

## Direction

Which side of a schema a description describes: what it **accepts**, or what it **produces**.

Only a schema that transforms has two sides worth telling apart, and those are the ones worth
describing carefully: a request body is the input side and a response body is the output side. We say
**direction**, and **input** and **output** for the two values; never "request" and "response", which
name one use of the distinction rather than the distinction. See
`docs/decisions/0012-a-description-has-a-direction.md`.

## Annotation

Documentation attached to a schema — a title, a sentence, an example, a note that it is deprecated.

An annotation never validates anything. It exists so that a generated document can say what the
rules cannot: why a field exists, what a refinement is checking, which of several acceptable forms
is preferred. We say **annotation** for this and **message** for the text of an error; they are both
prose and they are read by different people at different times.

## Document

A generated JSON Schema, in the 2020-12 dialect. Produced from a description, never from a schema
directly.

## Amendment

A caller-supplied function that may change one node of a generated document after it has been
written, and may declare that it has expressed what the document could not.

An amendment is the escape hatch for the rule only the caller can state — a refinement whose meaning
is a pattern this library has no way to discover. We say **amendment**, never "override", which in
C# means virtual dispatch and not this. See
`docs/decisions/0013-an-amendment-for-what-a-document-cannot-say.md`.

## Published document

A JSON Schema somebody else wrote, read back into a schema with `Theo.JsonSchema`.

The reverse of **document**, and for a different job: checking a payload against a description this
program did not author. What comes back validates a `JsonNode` and never produces a typed object,
which is what keeps it on the right side of
`docs/decisions/0001-validate-materialized-values.md`. See
`docs/decisions/0016-reading-a-document-back-into-a-schema.md`.

## Message provider

A caller-supplied function that turns the structured facts of an error into text, or declines and
lets the next provider answer. This is the extension point for localization.
