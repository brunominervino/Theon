# Theon

[![NuGet](https://img.shields.io/nuget/vpre/Theon?logo=nuget&label=NuGet)](https://www.nuget.org/packages/Theon)
[![Downloads](https://img.shields.io/nuget/dt/Theon?logo=nuget&label=downloads)](https://www.nuget.org/packages/Theon)
[![CI](https://github.com/brunominervino/Theon/actions/workflows/ci.yml/badge.svg)](https://github.com/brunominervino/Theon/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

Strongly typed, fluent schema definition, parsing and validation for .NET.

Zero dependencies. Native AOT friendly. A valid value parses without allocating.

> Status: early. The core is implemented and tested; the surface below is what exists today.

```csharp
using Theon;

private static readonly Schema<CreateUserRequest> UserSchema =
    Theo.Object<CreateUserRequest>()
        .Field(x => x.Name, Theo.String().Trim().MinLength(3).MaxLength(100))
        .Field(x => x.Email, Theo.String().Trim().ToLowerInvariant().Email())
        .Field(x => x.Age, Theo.Int().Min(18).Max(120))
        .Field(x => x.CompanyId, Theo.Guid().NotEmpty())
        .Field(x => x.Status, Theo.Enum<UserStatus>())
        .Field(x => x.Tags, Theo.Collection(Theo.String().NotEmpty()).MaxCount(10))
        .Field(x => x.CreatedAt, Theo.DateTime().RequireUtc().InPast());

var result = UserSchema.SafeParse(request);

if (!result.IsSuccess)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"{error.Path}: {error.Message}");   // Address.ZipCode: Invalid format.
    }
}
```

`Parse` throws a `SchemaValidationException` instead, for when a failure really is exceptional.
`IsValid` answers yes or no and stops at the first problem.

## Why another validation library

Because the interesting decisions can be made differently now.

**Errors are structured.** Every error carries a code, a path built from segments rather than a
pre-rendered string, and the declared bound that was breached. Messages are replaceable through a
provider chain, so nothing has to branch on English text to work out what went wrong.

**A valid value allocates nothing.** Not for a string, not for a number, not for a nested object.
The parse context lives on the stack, and the error list and the path are not created until
something actually fails. There is exactly one exception in the library and it is in the table below:
a set cannot be walked through an interface without boxing its enumerator, and unlike a list it has
no indexer to reach for instead.

| | Time | Allocated |
|---|---:|---:|
| `Theo.String().MinLength(3).MaxLength(100)` | 19 ns | 0 B |
| Flat object, four fields, valid | 170 ns | 0 B |
| Nested object, seven fields, valid | 264 ns | 0 B |
| Enum, checked against its declared members | 14 ns | 0 B |
| List of 20 e-mail addresses, valid | 2,599 ns | 0 B |
| Flat object, four fields, all invalid | 497 ns | 872 B |
| `Theo.String().Url()`, valid | 174 ns | 0 B |
| Discriminated union, second branch, valid | 91 ns | 0 B |
| Set of 10 tags, valid | 128 ns | 40 B |
| `Theo.String().Ipv6()`, valid | 30 ns | 0 B |
| Text to a number with bounds, valid | 27 ns | 0 B |

<sub>net10.0, x64. Reproduce with the benchmark command below; absolute numbers will differ by machine.</sub>

The same four rules written three ways, which is the comparison that answers "why would I switch":

| Four fields, valid | Time | Allocated |
|---|---:|---:|
| Theon | 246 ns | **0 B** |
| FluentValidation | 785 ns | 600 B |
| DataAnnotations | 1,704 ns | 2,288 B |

| Four fields, all invalid | Time | Allocated |
|---|---:|---:|
| Theon | 547 ns | 872 B |
| DataAnnotations | 3,066 ns | 3,104 B |
| FluentValidation | 7,836 ns | 9,336 B |

<sub>Read the times as the order of magnitude they are and not as precise figures. The run they
come from had a standard deviation of ten to twenty per cent, and the same Theon schema measured
246 ns there against 185 ns in the quieter run above — which is what a machine doing other work does
to a benchmark, and which means the ratios here understate rather than flatter. The allocation column
is an exact count and is unaffected by any of that. The comparison is in
<code>benchmarks/ComparisonBenchmarks.cs</code>, which also documents the ways it cannot be made
fair: Theon's e-mail pattern is stricter than either of the other two, so it is doing more work per
address, not less.</sub>

**No reflection anywhere.** `Field(x => x.Email, ...)` takes a plain delegate, and the property name
comes from the compiler through `CallerArgumentExpression`. There is no expression tree to interpret
and nothing to emit at run time, which is why Native AOT and trimming work without annotations.

**Lengths are counted the way a person counts.** `MinLength` and `MaxLength` measure Unicode code
points, so one emoji is one character rather than two.

**One numeric schema covers every numeric type**, through generic math, rather than one
hand-written schema per type. Non-finite values are rejected before any bound is considered, so a
`NaN` cannot slip past a range check that compares false in both directions.

**Enums are actually checked.** `(UserStatus)999` is a legal cast that no compiler will stop and no
deserializer will question, so an enum-typed value is not evidence that the value is a member of
it. `[Flags]` enums are validated by their bits, so `Read | Write` is accepted and an undeclared
bit is not.

**Conditional rules read as conditions.** A field that is optional in general but required once
another field takes a particular value is the most common cross-field rule there is, and the usual
way to express it — a single predicate over `!condition || requirement` — is a material implication
spelled as a disjunction, which almost everyone misreads. Two predicates say what is meant:

```csharp
Theo.Object<TaskItem>()
    .Field(x => x.Status, Theo.Enum<TaskStatus>())
    .When(
        x => x.Status == TaskStatus.Completed,
        x => x.CompletedAt is not null,
        x => x.CompletedAt,                       // the field the user has to fix
        "A completion date is required once the task is completed.");
```

When one condition unlocks several requirements, which is what usually happens, they go in a block
instead of restating the condition once per rule:

```csharp
    .When(x => x.Status == TaskStatus.Completed, rules => rules
        .Field(x => x.CompletedAt, Theo.DateTime().RequireUtc().Required())
        .Field(x => x.ClosedBy, Theo.String().NotEmpty().Required())
        .Field(x => x.Resolution, Theo.String().MinLength(10).Required()));
```

Rules inside the block are ordinary rules, not presence checks: a `CompletedAt` that is present but
local still fails `RequireUtc()`. `Required()` is the counterpart to `AllowNull()` — it gives a
`DateTime?` property a schema that refuses the null.

**Schemas can refer to themselves.** A comment with replies, a category with subcategories, a tree
of any shape:

```csharp
private static readonly Schema<Comment> CommentSchema =
    Theo.Object<Comment>()
        .Field(x => x.Body, Theo.String().NotEmpty())
        .Field(x => x.Replies, Theo.Collection(Theo.Lazy(() => CommentSchema)));
```

A value that contains itself reports an error instead of overflowing the stack, which is not
something a caller could have caught.

**Dictionaries too**, for the shapes whose keys are data rather than structure — translations by
language, prices by currency:

```csharp
Theo.Record(Theo.String().Length(3).Uppercase(), Theo.Decimal().Positive());
// a bad price for BRL reads as Prices.BRL
```

**And alternatives**, when a field may take one of several forms:

```csharp
Theo.OneOf(
    "Informe um e-mail ou um telefone.",
    Theo.String().Email(),
    Theo.String().Matches(PhoneNumber(), "phone"));
```

One message, not one per rejected branch: "not an e-mail, and not a phone number" is two
complaints about a field where the reader wanted one.

**A closed hierarchy dispatches on its type.** A discriminated union, written the way C# already
writes one:

```csharp
Theo.Subtypes<Payment>()
    .Case(Theo.Object<PixPayment>().Field(x => x.Key, Theo.String().NotEmpty()))
    .Case(Theo.Object<CardPayment>().Field(x => x.Number, Theo.String().Length(16)));
// a Pix with no key reads as Key: Informe pelo menos 1 caractere.
```

Zod reads a literal property here, because TypeScript erased the type and so the discriminator has
to be data. In C# the discriminator *is* the type: `System.Text.Json` already chose which subtype to
construct before any schema ran. So the branch is picked by a type test — faster than trying every
alternative, and the error names the branch rather than saying the value was none of three things.

A branch that no value could reach, because an earlier one already covers its type, is refused when
the schema is built rather than left to silently never fire. Where the model is instead one flat
class with a `Kind` property, `When` is the tool and nothing new is needed.

**Two schemas can be required at once**, which is what a platform rule plus a tenant's extra rule
looks like:

```csharp
Schema<string> both = PlatformRules.And(TenantRules);
```

Both see the same value and both report, so a caller sees every reason at once. That is an
intersection; feeding one schema's result into the next is `Transform`, which says so in its type.

**Formats for the things people actually type.** `Email()`, `Url()`, `Uuid()`, `Base64()`,
`Base64Url()`, `Hex()`, `E164()`, `Iso8601()`, `Iso8601Date()`, `Iso8601Time()`,
`Iso8601Duration()`, `Ipv4()`, `Ipv6()`, `Cidr()`, `Hostname()`, `Jwt()`, `CreditCard()` and
`Iban()`. Every pattern is a `[GeneratedRegex]` declared
`NonBacktracking`, so matching is linear in the length of the input by construction and a hostile
string cannot be made to cost anything — there is a test that tries.

Four of them are not patterns at all, for two different reasons. `Ipv4()` and `Ipv6()` are hand-written
scans over a span, because a pattern covering IPv6 needs an automaton of about two thousand two hundred
states and the non-backtracking engine refuses to build one over a thousand — so the linear-time
guarantee is simply not available for that shape. They turned out to be about five times faster than
the patterns as well. `CreditCard()` and `Iban()` are checksums, Luhn and mod-97, which is the point:
a pattern counts digits, while a checksum catches the single mistyped digit and the two transposed ones
that people actually produce.

Matching the specification is explicitly not the goal. `Url()` refuses `javascript:`, refuses
credentials before the host, and refuses a single-label host, because a field labelled "website"
that accepts the first is a vulnerability and the other two are mistakes. `Matches()` with your own
pattern is the documented escape hatch.

`Iso8601Date()` is not a pattern at all. A pattern can describe the shape of a date and cannot tell
February from the number 31, so this one parses: `2026-02-31` fails. `Iso8601Duration()` is not one
either, because the ordering is the rule — `P1M2Y` and `PT1D` are both wrong, and the same letter means
months before the `T` and minutes after it.

**Text can become a value, and the value can have rules.** The most ordinary pipeline there is:

```csharp
Schema<string, int> pageSize = Theo.String().Trim()
    .TryTransform<int>(int.TryParse, "Must be a whole number.", Theo.Int().Min(1).Max(100));
```

A transformation used to have to be the last thing in a chain, which left every query parameter, form
field and configuration value unsayable. `TryTransform` takes a conversion shaped like `TryParse` — so
`int.TryParse` is handed over as it is, with no lambda around it — and reports a failed conversion
instead of throwing. A failure after the conversion reports at the path the value *came from*, so a
page size out of range is reported against `PageSize` and not against the number it became.

**A rule can report for itself.** `Refine` with a predicate answers yes or no and produces one error.
Where that is not enough — two distinct complaints, a failure that belongs against one property, a code
a caller can branch on — the rule can be handed the parse instead:

```csharp
Theo.Object<SignUp>().Refine(static (SignUp signUp, ref ParseContext context) =>
{
    if (signUp.Password != signUp.PasswordConfirmation)
    {
        context.PushProperty(nameof(SignUp.PasswordConfirmation));
        context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom },
                         "The two passwords do not match.");
        context.Pop();
    }
});
```

**A value can stand in for an absent one.** `Default(x)` answers with `x` where the value was null;
`Catch(x)` swallows a failure and answers with `x`; `Literal(x)` requires exactly `x`.

```csharp
var pageSize = Theo.Int().Min(1).Max(100).Default(20).Parse(query.PageSize);   // 20 when null
```

Both shape the value a parse *produces*, so they belong where that value is read. An object schema
checks an instance rather than rebuilding one and never writes to it, so a default on a field would
be computed and dropped — C# already has a property initializer for that, and it is better.

**Schemas describe themselves.** A schema knows its types, its bounds and its formats, which is
most of a JSON Schema document already:

```csharp
var document = Theo.Object<CreateUserRequest>()
    .Field(x => x.Email, Theo.String().Email().Annotate(description: "Where we write to you."))
    .Field(x => x.Age, Theo.Int().Min(18).Max(120))
    .ToJsonSchema(new JsonSchemaOptions { Title = "CreateUserRequest" });

Console.WriteLine(document.ToJsonString());
```

The dialect is JSON Schema 2020-12, which is the one OpenAPI 3.1 uses, so the result drops straight
into an OpenAPI description. A recursive schema is written once under `$defs` and referred to by
`$ref`. `Annotate` carries the title, description, example and deprecation that rules cannot express.

What a document will never contain is a guess. A `Refine` is an arbitrary predicate with no keyword
to map to, and a date range has no keyword that applies to a string, so both are left out: a
document that omits a rule is incomplete, and one that states a rule nothing enforces is wrong.

And you can ask to be told which ones. Where the document is the contract rather than the
documentation — because the client generated from it will be the only thing checking — a silently
missing rule is a rule nothing enforces:

```csharp
schema.ToJsonSchema(new JsonSchemaOptions { OnUnrepresentable = UnrepresentablePolicy.Throw });
// Email: RefineCheck
// CheckIn: a minimum bound, which has no keyword for a string
```

And where the rule is one only you can state, you can state it. An amendment receives every node of
the document once it is written, with the path it is at and the list of what it could not say:

```csharp
schema.ToJsonSchema(new JsonSchemaOptions
{
    OnUnrepresentable = UnrepresentablePolicy.Throw,
    Amend = node =>
    {
        if (node.Path == "ZipCode")
        {
            node.Json["pattern"] = @"\A\d{5}-\d{3}\z";
            node.Expressed = true;   // and the policy stops reporting this node
        }
    },
});
```

`Expressed` is a declaration, and leaving it alone still reports however much you changed: nothing
here can tell a `pattern` that expresses a refinement from one that does not, and a document believed
to be complete that quietly is not is the whole thing that policy exists to prevent. The schema
instance is deliberately not handed over — it would arrive as an `object` to pattern-match, which is
the branching on schema identity this library refuses in its own code.

**And the model underneath is yours.** A schema describes itself by overriding `Describe`, which is
how a schema you wrote gets documented as well as the built-in ones:

```csharp
public sealed class UlidSchema : Schema<string>
{
    public override bool TryParse(ref ParseContext context, string input, out string output) { ... }

    public override SchemaDescription Describe(DescriptionContext context) => new()
    {
        Kind = SchemaKind.String,
        Format = "ulid",
        MinLength = 26,
        MaxLength = 26,
    };
}
```

And the same model is what you read to write a generator this library does not ship — for protobuf,
for Avro, for rendering a form. `ToJsonSchema` is one such generator and has no more access to a
schema than this gives you:

```csharp
var described = schema.Describe();

Write(described.Root);                                  // kinds, bounds, formats, children
foreach (var (name, definition) in described.Definitions)   // whatever repeated, named once
{
    WriteNamed(name, definition);
}
```

**A document has a side.** A schema that transforms accepts one shape and produces another, and a
request body and a response body are the two different documents that come out of it:

```csharp
var schema = Theo.String().Trim()
    .TryTransform<int>(int.TryParse, "Must be a whole number.", Theo.Int().Min(1).Max(100));

schema.ToJsonSchema();                                 // { "type": "string" }
schema.ToJsonSchema(new JsonSchemaOptions { Direction = DescriptionDirection.Output });
                                                       // { "type": "integer", "minimum": 1, ... }
```

`Input` is the default, because that is what a request body needs and what every document generated
before this option existed said. The output side is where the follow-on schema earns its keep: with
one, the converted value describes itself completely and nothing is left out; without one there is a
type and no schema, so the document says `integer` and admits that is all it knows.

Six wrappers differ between the two sides, and two that look as though they should do not.
`Required()` is declared `Schema<T?>` and never produces a null, because the only null that leaves it
belongs to a parse that failed. `Catch()` is the one case where the output side is the *more* precise
of the two: `Theo.String().Email().Catch("none")` really does produce `"none"`, so the response side
says `anyOf: [an e-mail address, const: "none"]` rather than claiming an address it may not deliver.

For an OpenAPI description, `ToJsonSchemaDocument` hands the shared schemas back rather than inlining
them, so they can go in `components/schemas` where every operation can reach them. In ASP.NET Core,
`.Validate(schema)` leaves the schema on the endpoint as metadata, so whatever produces the
description can ask each endpoint what it actually requires instead of inferring a body from the
handler signature. There is deliberately no document transformer in the box: the built-in OpenAPI
pipeline arrived in .NET 9, this package also targets net8.0, and metadata serves every generator
without committing to one.

**And a document somebody else wrote becomes a schema.** The other direction, for checking a payload
against a description this program did not author — a third party's OpenAPI description, a contract
test, a configuration file whose shape is declared elsewhere:

```csharp
var published = JsonNode.Parse(await client.GetStringAsync(schemaUrl))!;
var schema = Theo.JsonSchema(published);

var result = schema.SafeParse(JsonNode.Parse(payload));
// Errors arrive with paths, codes and messages like any other parse.
```

It hands back a `Schema<JsonNode?>` and never a typed object: a document describes JSON, and turning
that into a type would mean matching property names by reflection. Where you have a type, keep using
`Theo.Object<T>()` — this is for where you do not.

A keyword it cannot honour — `oneOf`, `not`, `if`, `patternProperties` — throws when the schema is
built, rather than being dropped. Dropping an assertion would make the schema accept values the
document rejects, which is the direction that lets a bad value through while you believe it was
checked. A keyword that asserts nothing is ignored, as the dialect requires.

The two directions are checked against each other rather than asserted: a document generated from a
schema, read back, and generated again has to be the same document, for every shape this library
writes.

**Errors arrive in the shape you render.** Grouping a flat list by field is a loop every caller
would otherwise write, and get subtly wrong at the root level:

```csharp
var flat = result.Errors.Flatten();

return TypedResults.ValidationProblem(
    flat.ToDictionary().ToDictionary(p => p.Key, p => p.Value.ToArray()));
// { "Email": ["Invalid e-mail address."], "Recipients[1]": ["..."] }
```

`ToTree()` gives the nested form instead, where each component receives the subtree for the value
it is drawing and never parses a path string to find out whether something below it failed.
Elements are keyed by index, so one bad row in two hundred costs one entry rather than two hundred.

`ToPrettyString()` is the third shape, for the place where a person reads them and no structure
helps — a log, a console, a failing test:

```
Name: Must be at least 3 character(s) long.
Email: Invalid e-mail address.
```

**Rules that need a round trip can have one.** Whether an address is already registered is not a
question the value can answer, and it is one of the most commonly needed validations there is:

```csharp
var schema = Theo.Object<SignUp>()
    .Field(x => x.Email, Theo.String().Trim().Email()
        .RefineAsync((email, ct) => users.IsAvailableAsync(email, ct),
                     "That address is already registered."));

var result = await schema.SafeParseAsync(request, cancellationToken: ct);
```

The synchronous path is untouched by this and still allocates nothing; asynchrony is a second path
that every existing schema joins for free. The rule runs only after everything before it passed, so
a malformed address never costs a database round trip — and a schema holding one refuses to be
parsed synchronously rather than blocking a thread to hide the difference.

**Durations, addresses and sets have schemas too.** `Theo.TimeSpan()` (whose `Positive()` catches
two dates subtracted the wrong way round), `Theo.Uri()` for a value that already is one, and
`Theo.Set()` for a `HashSet<T>` or an `IReadOnlySet<T>`. `Unique()` on a list reports each repeat at
its own index, so a form can mark the row.

A set reports a member's failure at the set's own path and never at an index: enumeration order is
not stable, so `Tags[2]` would name a different member on the next run. It is also the one schema
that allocates on the success path — exactly once, because walking a set through an interface boxes
its enumerator and a set has no indexer to use instead.

A tuple needs nothing new: `Theo.Object<(string Name, int Age)>()` already works, and so does a
`char` — as `Theo.OneOf("...", Theo.Literal('Y'), Theo.Literal('N'))`.

**Objects can be structs.** `Theo.Object<T>()` takes a class, a record, a struct or a record
struct. A struct parses within noise of the equivalent class and neither allocates.

**Clock-dependent rules take a clock.** `InPast()` and `InFuture()` read a `TimeProvider`, so a
test pins the instant instead of sleeping or racing midnight. `RequireUtc()` turns the
`DateTimeKind` trap — where a local and a UTC value compare as if on the same clock and silently
disagree by the machine's offset — into a validation failure at the boundary.

## Installing

```bash
dotnet add package Theon --prerelease
dotnet add package Theon.AspNetCore --prerelease         # optional, for ASP.NET Core
dotnet add package Theon.Localization.PtBr --prerelease  # optional, messages in Portuguese
dotnet add package Theon.Localization.Es --prerelease    # optional, Spanish
dotnet add package Theon.Localization.Fr --prerelease    # optional, French
dotnet add package Theon.Localization.De --prerelease    # optional, German
dotnet add package Theon.Localization.It --prerelease    # optional, Italian
dotnet add package Theon.Localization.Nl --prerelease    # optional, Dutch
dotnet add package Theon.Localization.Pl --prerelease    # optional, Polish
```

`Theon.AspNetCore` is a separate package so the core keeps its promise of no dependencies: a worker
service or a console app validating payloads should not drag ASP.NET Core in behind it. The two
version in lockstep, so there is never a question of which release of one works with which release
of the other.

The `--prerelease` flag is not optional: every release so far is a preview, and NuGet will not
install one unless asked. That is the point of shipping previews — the public API is still moving,
and a stable version number is a promise this library is not ready to make.

## Building

```bash
dotnet restore Theon.slnx
dotnet build Theon.slnx -c Release
dotnet build tests/Theon.Tests/Theon.Tests.csproj -c Release -t:Test
```

Tests run through the MSBuild `Test` target rather than `dotnet test`; `AGENTS.md` explains why.

Benchmarks:

```bash
dotnet build benchmarks/Theon.Benchmarks/Theon.Benchmarks.csproj -c Release
./artifacts/bin/Theon.Benchmarks/release/Theon.Benchmarks.exe --filter "*" --memory
```

Packaging:

```bash
dotnet pack src/Theon/Theon.csproj -c Release
```

The package is produced under `artifacts/package/release/`, as a prerelease by default. See
[`docs/releasing.md`](docs/releasing.md) for versions, verification, and publishing.

## ASP.NET Core

```csharp
private static readonly Schema<CreateUser> CreateUserSchema =
    Theo.Object<CreateUser>()
        .Field(x => x.Email, Theo.String().Trim().Email())
        .Field(x => x.Age, Theo.Int().Min(18));

app.MapPost("/users", (CreateUser request) => Results.Ok())
   .Validate(CreateUserSchema);
```

A failure answers 400 with `ValidationProblemDetails`, keyed by rendered path, and the handler is
never called:

```json
{
  "status": 400,
  "errors": {
    "Email": ["Invalid e-mail address."],
    "Age": ["Must be greater than or equal to 18."]
  }
}
```

The schema is named at the endpoint rather than discovered from the container. Both work; this one
puts the rule where the endpoint is declared, so a reader sees what it accepts and what it requires
in the same three lines — and the compiler checks the schema matches the type the handler takes.

Validation runs asynchronously, so a schema with a `RefineAsync` rule works here with no extra
ceremony, and the request's cancellation token reaches it.

**Uploaded files have a factory of their own.** `IFormFile` is an ASP.NET Core type, so a schema for
one cannot be built from `Theo`, which lives in a package with no dependencies and is keeping it:

```csharp
app.MapPost("/avatar", (IFormFile file) => Results.Ok())
   .Validate(Upload.File()
       .MaxSize(5 * 1024 * 1024, "Pick an image no larger than 5 MB.")
       .ContentType("image/png", "image/jpeg")
       .Extension(".png", ".jpg", ".jpeg"));

Upload.Files()
    .MinCount(1)
    .MaxCount(10)
    .MaxTotalSize(20 * 1024 * 1024)      // the one rule no per-file limit can express
    .Each(Upload.File().ContentType("application/pdf"));
```

**The size is the only fact among these.** `ContentType` and `FileName` come from the headers of the
multipart section, written by whoever sent the request and forged in a line. These rules catch
somebody picking the wrong file, which is the common failure, and they are not a security control —
a check a reader believes in and which is not true is worse than no check. Proving what a file is
means reading its opening bytes, which is deliberately out of scope and is what `RefineAsync` is for.

A document writes a file as `{"type": "string", "contentMediaType": "image/png"}`, which is what
OpenAPI 3.1 took for a binary part. The size bound has no keyword at all — `maxLength` counts
characters and a binary section has none — so it is reported as something the document cannot say
rather than spelled with a keyword that means something else.

## Messages in another language

The core decides *what* went wrong; a provider decides how to say it. That is why no language is
built in, and why adding one changes nothing in the core:

```csharp
// Once, at start-up:
SchemaGlobalOptions.MessageProvider = BrazilianPortugueseMessages.Provider;
```

```
Nome:  Informe pelo menos 3 caracteres.
Email: E-mail inválido.
Idade: Deve ser maior ou igual a 18.
```

A provider returns `null` for anything it does not describe, so it can cover the cases it cares
about and let the rest fall through. Writing one for another language is one method over
`ValidationErrorInfo` — no resource files, no satellite assemblies.

Seven languages ship: Portuguese, Spanish, French, German, Italian, Dutch and Polish. Polish is the
one that shows what the mechanism is worth — it has three plural forms, and which one a number takes
depends on its last two digits, so twelve takes a different form from twenty-two. Nothing in the core
changed to allow that, because a provider is a function from facts to a sentence and how it decides is
entirely its own business.

Every provider has a test asserting it answers for *every* error code and *every* format the library
reports, so a new code cannot ship with half the languages quietly falling back to English.

## Design

The decisions that shaped the library, and the reasoning behind each, are in
[`docs/decisions/`](docs/decisions/). The vocabulary is in [`CONTEXT.md`](CONTEXT.md).

Theon is inspired by [Zod](https://github.com/colinhacks/zod), and owes it most of the
problems it knows to solve. It is an independent implementation: no code was taken, and where Zod's
design answers a question C# does not ask, the answer was dropped rather than translated.

## Target frameworks

`net8.0` and `net10.0`. See [`docs/decisions/0006-target-frameworks.md`](docs/decisions/0006-target-frameworks.md).

## The name

Theon, for Theo — the maintainer's son. The factory is spelled the same way, so his name reads on
every line that builds a schema:

```csharp
Theo.String().Trim().Email()
```

Not `T`, tempting as it was: a type named `T` is shadowed by the type parameter inside any generic
that follows the usual convention, and the call fails to compile with CS0704.

## Licence

MIT. See [LICENSE](LICENSE).
