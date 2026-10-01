# 13. An amendment for what a document cannot say

Status: Accepted

Amends: 0009 (describe through a reified model).

## Context

Decision 9 settled that a document never guesses. A rule with no keyword in the dialect contributes
nothing, because a document that omits a rule is incomplete and one that states a rule nothing
enforces is wrong. `UnrepresentablePolicy.Throw` exists so that a caller whose document is the
contract finds out which rules were left out.

Being told is where it stopped. The policy names a rule and gives no way to express it, and the
caller is frequently the one person who could: a `Refine` that checks a postcode against a national
format has a `pattern` that says exactly what it means, and nothing in this library can discover what
that pattern is. The only honest advice was `Annotate`, which puts the rule in prose that no client
generator reads.

The same gap covers everything the dialect has no word for and the surrounding document wants: a
vendor extension, a hint for whatever renders the form, a `discriminator` for an OpenAPI union.

Zod solves this with an `override` callback receiving `{ zodSchema, jsonSchema, path }`.

## The problem underneath

The policy was checked on the *description*, before a single node had been written, and an amendment
necessarily acts on the *document*. So a caller who patched a node to express a rule still took the
exception for not having expressed it. Two ways out were available:

**Reorder.** Write first, check the policy afterwards. This does not work on its own: the policy
reads the description, and an amendment changes the document. Reordering alone would report exactly
the same rules in exactly the same places, one step later.

**Let the amendment say so.** Give it a way to report that it has expressed a node's rules itself,
and exempt that node from the report.

## Decision

Both, because the second needs the first. The document is written, with the amendment running as each
node is finished, and only then is what is still missing reported. Nothing is handed back before the
report, so a document that fails the policy is still never returned.

```csharp
public delegate void JsonSchemaAmendment(JsonSchemaNode node);

public sealed class JsonSchemaNode
{
    public string Path { get; }
    public JsonObject Json { get; }
    public IReadOnlyList<string> Unrepresentable { get; }
    public bool Expressed { get; set; }
}
```

**Expressing is per node, not per rule.** A caller who patched a node knows what they patched it for,
and the writer cannot tell a `pattern` that expresses a refinement from one that does not. Saying
nothing still reports, which is the conservative direction: the failure this policy exists to prevent
is a document believed to be complete that quietly is not.

**The schema instance is not exposed**, which is the one place this departs from Zod. It would have
to arrive as `object`, because its type parameters are not knowable at the delegate's signature, and
an `object` the caller has to pattern-match is precisely the branching on schema identity this
repository refuses in its own code — handing it to callers would be worse than writing it ourselves.
It would also fix the name of every internal wrapper type as a compatibility obligation for ever,
which is a far larger commitment than the description model itself. The path, the written node and the
list of what the node could not say are enough to aim at.

**Nodes are offered children first.** An amendment on an object sees its fields as they will be read,
including whatever an amendment on one of those fields did to them.

## Consequences

- Writing and reporting became one walk. They were two — a writer and an `UnrepresentableWalk` — and
  each built its own paths and asked `Representable` its own questions. A node cannot be offered its
  own losses unless the thing writing it is the thing that knows them, so merging them was not
  optional; it also removed a drift hazard that `Representable` existed to mitigate rather than
  remove. `UnrepresentableWalk` is gone.
- The paths an amendment sees are the paths the report uses, necessarily, because there is now one
  construction of them. `Address.ZipCode`, `Recipients[]`, `*` for a map's value, `$defs/Comment`,
  and empty at the root.
- A branch of an `anyOf` or an `allOf` shares its parent's path, since it sits at the same position as
  the value itself. An amendment telling two branches apart does so by looking at the node. This was
  already true of the report's paths and is now documented rather than incidental.
- A `$ref` node is offered with nothing to express, because it has no rules of its own. It is still
  offered, because 2020-12 allows an annotation beside a reference and a caller may want one.
- The definitions are written whether they end up inside the document or beside it, so an amendment
  sees every node exactly once either way.
- The exception message now names three ways out instead of two.

## Trade-offs

An amendment can make a document say anything, including something false. That was already reachable
— `ToJsonSchema` returns a mutable `JsonObject` precisely so a caller can add to it — and what
changes is that it can now be done per node, with the paths and the losses in hand, instead of by
walking the result afterwards and rediscovering both. The judgement is the same one decision 11 made
about handing out the parse context: an escape hatch out of proportion to the task gets used badly or
not at all, and that is worse than one that can be misused.

`Expressed` is a flag a caller can set wrongly, and then the policy stops reporting a rule that is
still not in the document. There is no way to check the claim — checking it would mean understanding
what the caller's refinement means, which is the thing this library cannot do and the reason the hook
exists. The flag is a declaration, and the default is to disbelieve.
