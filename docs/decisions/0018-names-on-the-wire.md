# 18. Names on the wire, not names in the source

Status: Accepted

## Context

Three names reached a caller as the C# identifier, and none of the three is what the caller sees on
the wire.

**An error path.** `ZipCode`, while the application serialized `zipCode`. A form keyed by the
response could not match the body it had sent.

**A document property.** `properties: { "ZipCode": … }`, so a client generated from the document
looked for a field the server never sends.

**An enum member.** `enum: ["Editar"]`, while the application serialized `editar` — so the one list a
generated client had to match against was the one list that was wrong, and every request it built
carried a value the server rejected.

A fourth name was being dropped rather than mis-spelled: `Matches(pattern, "cnpj")` carried the name
as far as the error, which reads `Invalid cnpj.`, and wrote only the pattern into the document.

## Decision

A policy, in each of the places the decision is actually made, and the serialized name wherever
something wrote one down.

### Where the policy lives

`ParseOptions.PropertyNamingPolicy` and `SchemaGlobalOptions.PropertyNamingPolicy` for an error path;
`JsonSchemaOptions.PropertyNamingPolicy` for a document. `null` everywhere by default, which is the
C# member name and what every release so far produced.

Two halves rather than one, because they are configured at different times by different people: a
parse happens per request and a document is generated once, and an application may well want a
document in camelCase while a test asserts paths by member name.

### Where the renaming happens

The path half renames in `ParseContext.CapturePath` and nowhere else. That is the one moment a path
becomes something anybody reads, and it only happens once an error has already decided to allocate —
so a valid value pays nothing, and `ToString`, `Flatten`, `ToTree` and the ProblemDetails dictionary
all agree without any of them being told a policy exists.

The alternative was an overload on each of those four taking a policy, which would have put the
decision in the caller's hands at every render site and let two of them disagree about the same error.

The document half renames in the field binding, which is the one place a property is named. The
writer, the paths an `Amend` targets and the unrepresentable report then read the same name.

An index is a position rather than a name, so nothing happens to it. A map's keys are values rather
than members and are whatever the data holds, so nothing happens to them either.

### Enum members are named separately

`JsonSchemaOptions.EnumMemberNamingPolicy`, not the property policy.

`System.Text.Json` configures the two independently — properties through `JsonSerializerOptions` and
enum members through `JsonStringEnumConverter` — so an application may well camelCase its properties
and leave its enum members alone. Reading one off the other would put a wrong `enum` list into the
document for exactly that application, and a wrong `enum` list becomes a wrong request body in a
generated client. Convenience is not worth buying with a chance of being wrong in the one place that
is hardest to notice.

A member carrying `[JsonStringEnumMemberName]` keeps the name it was given: somebody who wrote a name
down meant that name and not a transformation of it, which is how the converter treats it too.

### The attribute is matched by name

`JsonStringEnumMemberNameAttribute` arrived in .NET 9 and this library also targets `net8.0`, where
the type does not exist. It is found through `CustomAttributeData` by comparing
`AttributeType.FullName`, which costs one string comparison per member, once per enum type, in a
static initializer that never runs during a parse.

The alternative was `#if`, and there is not one in `src`. Decision 6 took the target frameworks down
to two partly to avoid conditional code diverging in the hottest paths, and that is a property worth
more than the clarity of a direct reference.

Reading enum fields needs a trim suppression, the second in this repository. The trimmer does not
remove an enum's fields — they are its values — and annotating `TEnum` instead would push the
requirement onto `Theo.Enum` and from there onto every caller, for a guarantee the runtime already
makes. If it were ever untrue, the attribute is simply not found and the C# name is used, which is the
behaviour before this existed.

### A caller's format name goes to `format`

Both `format` and `pattern` are written for a name the caller chose. 2020-12 puts `format` in an
annotation vocabulary and requires an unrecognised value to be *ignored* rather than to fail, so a
name of the caller's own is safe there by specification; an `x-format` of our own invention would be
equally safe and nothing would read it. The two are written together because they are different
things: the pattern is the rule a validator enforces and the format is only its name.

Names this library chose for its own rules are unchanged, which needed telling apart from names the
caller chose — they arrive at the same check as the same kind of string. `format: "lowercase"` says
nothing to any tool, and `base64` has a spelling of its own in this dialect that is not `format`.

## Consequences

- Four public members for the policies and one more on `DescriptionContext`, so a custom schema that
  names properties of its own can apply the same one. A document spelling half its keys one way and
  half the other is worse than one spelling them all the long way.
- A camelCase document still survives the round trip through `Theo.JsonSchema`, because the renaming
  happens before anything is written and the reader has no opinion about names.
- `SchemaGlobalOptions.PropertyNamingPolicy` is process-wide mutable state, which is the second such
  property after the message provider. Its test runs in a collection of its own: xunit runs classes
  in parallel and a test elsewhere read the policy it had set, which is the same hazard the API has.
- Every document this library already produced is byte-identical unless a policy is set, with the one
  exception of a caller-named pattern, which was losing information.

## Trade-offs

Two naming policies where an application has one idea of how it names things, and it has to say so
twice — three times counting the enum one. The alternative was one knob that is sometimes wrong, and
the places it would be wrong are a document generator's output, which nobody checks by hand.

The policies are `JsonNamingPolicy`, which is a `System.Text.Json` type. That is a dependency this
library already has — it is in the shared framework and the document generator returns a `JsonObject`
— and inventing a naming abstraction of our own so that the signature mentions no serializer would
make every caller adapt the one they already have.
