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
something actually fails.

| | Time | Allocated |
|---|---:|---:|
| `Theo.String().MinLength(3).MaxLength(100)` | 19 ns | 0 B |
| Flat object, four fields, valid | 170 ns | 0 B |
| Nested object, seven fields, valid | 264 ns | 0 B |
| Enum, checked against its declared members | 14 ns | 0 B |
| List of 20 e-mail addresses, valid | 2,599 ns | 0 B |
| Flat object, four fields, all invalid | 497 ns | 872 B |

<sub>net10.0, x64. Reproduce with the benchmark command below; absolute numbers will differ by machine.</sub>

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

**Objects can be structs.** `Theo.Object<T>()` takes a class, a record, a struct or a record
struct. A struct parses within noise of the equivalent class and neither allocates.

**Clock-dependent rules take a clock.** `InPast()` and `InFuture()` read a `TimeProvider`, so a
test pins the instant instead of sleeping or racing midnight. `RequireUtc()` turns the
`DateTimeKind` trap — where a local and a UTC value compare as if on the same clock and silently
disagree by the machine's offset — into a validation failure at the boundary.

## Installing

```bash
dotnet add package Theon --prerelease
dotnet add package Theon.AspNetCore --prerelease   # optional, for ASP.NET Core
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
