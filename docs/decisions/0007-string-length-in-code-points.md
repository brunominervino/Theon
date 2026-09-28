# 7. Measure string length in Unicode code points

Status: Accepted

## Context

`MinLength` and `MaxLength` have to decide what a character is. The obvious
answer, `string.Length`, counts UTF-16 code units. Under that definition a
single emoji counts as two, so a user who typed one character into a field
limited to one is told the field is too long. This is a real and frequently
reported class of bug, and Zod changed its own behaviour to fix it.

Three units are available:

- **UTF-16 code units** (`string.Length`). Free, and wrong for any text outside
  the Basic Multilingual Plane.
- **Code points.** A surrogate pair counts once. Requires a scan, but the result
  is stable everywhere.
- **Grapheme clusters** (`StringInfo`). Closest to what a person perceives as a
  character, since it also collapses a base letter plus its combining marks.

## Decision

Code points.

## Consequences

- One emoji counts as one, which is what the user who typed it believes.
- A base letter plus a combining mark counts as two. `"é"` has length 2
  even though it renders as a single glyph.
- An unpaired surrogate counts as one.
- Lengths are measured lazily. A string whose UTF-16 length already satisfies
  the bound cannot fail it, because the code-point count is never larger, so the
  common all-ASCII case never scans the string. `MinLength` needs the same
  reasoning in reverse: a string with at least twice the required units cannot
  be short whatever its encoding.

## Trade-offs

Grapheme clusters would match human perception more closely still, and they are
the reason this decision is worth recording rather than assuming. They were
rejected because segmentation depends on the Unicode version and the ICU data
shipped with the host. The same schema and the same input would then accept on
one machine and reject on another, and a validation library whose answers move
under it is worse than one whose definition of a character is slightly coarse.
Code points are defined by the standard and do not drift.

Callers who genuinely need grapheme counting can express it with `Refine` and
`StringInfo`, and accept the portability consequence explicitly.
