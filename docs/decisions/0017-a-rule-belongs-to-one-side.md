# 17. A rule is claimed only on the side it was checked against

Status: Accepted

Amends: 0012 (a description has a direction).

## Context

Rules run in the order they were written, and a normalization rewrites the value for everything after
it. So a rule written after one is checked against the normalized value and not against what arrived.

The document claimed it of the input anyway:

```csharp
Theo.String().Trim().MaxLength(14)          // accepts "   12345678901234   "
Theo.String().Trim().ToUpperInvariant().Matches(UpperCaseOnly)   // accepts "  abc  "
Theo.String().Trim().Email()                // accepts "  ada@example.com  "
```

Each of those produced a document asserting the post-normalization constraint of the request body. A
client generated from it refused values the endpoint accepts, which is how this was found: an
application reported its own clients rejecting a tax number because it had spaces at the ends and was
in lower case — both of which the server fixes before it checks anything.

Decision 12 said a normalization is not modelled and described both sides identically. Being silent
about the normalization itself was right. Carrying the rules *after* it onto the input side was the
gap, and decision 12 did not notice it because it was reasoning about what a normalization adds
rather than about what it invalidates.

The direction that matters is asymmetric. A document weaker than the server is **incomplete**: a
client sends something the server rejects, gets an error, and the error is right. A document stronger
than the server is **wrong**: a client refuses to send something the server would have accepted, and
no error ever reaches anybody who could diagnose it. That is the same ordering decision 9 settled —
omitting a rule is incomplete and stating one nothing enforces is wrong, and wrong is worse.

## Alternatives

**Narrow each surviving rule.** `Trim().Matches(p)` could become `pattern: \s*p\s*`. Exact for `Trim`
and expressible only for a pattern following a normalization whose effect is known. `Normalize` takes
an arbitrary `Func<string, string>` and guarantees nothing, so this has no general form.

**Keep per-normalization knowledge.** `Trim` only shortens, so a lower bound written after it does
still hold of the input; `ToUpperInvariant` preserves length, so both bounds survive it. Sound where
it applies, and it is a matrix of normalization against rule — exactly the pile of special cases this
repository refuses, with the additional property that the document's completeness would then depend
on which of two interchangeable spellings the author reached for.

**A third direction.** `Input`, `Output`, and something meaning "input, strictly". Three directions to
explain and two of them nearly the same.

**Drop, uniformly.** Claim a rule only on the side of the value it was actually checked against.

## Decision

Drop, uniformly.

- On the **input** side, the rules after the last normalization are not claimed.
- On the **output** side, it is the rules before it: `MinLength(3).Trim()` accepts `"  a  "` and
  produces a single character, so a lower bound written before the trim is not true of what comes out
  either.
- A normalization claims nothing itself, which was already true, and already records that it could
  not be expressed.

What is dropped is recorded rather than discarded, so `UnrepresentablePolicy.Throw` names it and a
caller whose document is the contract is told the constraint is not in it.

Expressed as `Check.Rewrites`, a property every rule answers, rather than as a test for
`OverwriteCheck`. A list of rule types in shared code is what this repository refuses, and this is
shared code by construction: it runs for every schema that has rules at all.

## Consequences

- `Trim().Email()` — the chain this library's own README recommends — publishes `{"type": "string"}`
  for a request body. That is the honest price and it is a real loss.
- Two ways back, both already in the library. `Direction.Output` describes the exact rules, which is
  right for a response document and for a request document whose clients are known to tolerate the
  untrimmed form. `Amend` puts any single keyword back, with `Expressed` to say the claim was
  deliberate.
- The division now reads coherently: a request document is permissive because its job is to tell
  somebody what will be accepted, and a response document is precise because its job is to tell
  somebody what they will receive.
- Two existing tests changed, both asserting the old claim. Neither was weakened: one kept its subject
  and gained what the normalization now also takes away, and the other moved to the output side, where
  its claim that the metadata describes the rules the filter enforces is still exactly true.

## Trade-offs

A lower bound written after `Trim` is given up for nothing: it does still hold of the input. That is
the cost of having one rule instead of a table, and the table is the thing that rots — a normalization
added later would need a row in it for every rule, and the row nobody adds is a silent wrong claim
rather than a loud missing one.

Somebody will read the new request document, see `{"type": "string"}`, and conclude the library
forgot their rules. The report names every one of them, and this record is what the report's reader
is pointed at.
