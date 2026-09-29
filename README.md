# Theon

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

**Clock-dependent rules take a clock.** `InPast()` and `InFuture()` read a `TimeProvider`, so a
test pins the instant instead of sleeping or racing midnight. `RequireUtc()` turns the
`DateTimeKind` trap — where a local and a UTC value compare as if on the same clock and silently
disagree by the machine's offset — into a validation failure at the boundary.

## Installing

Not yet published to NuGet.

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
