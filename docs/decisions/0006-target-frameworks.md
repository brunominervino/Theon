# 6. Target net8.0 and net10.0 only

Status: Accepted

## Context

The library needs to decide how far back to reach. The candidates were
`netstandard2.0`, `netstandard2.1`, and the current .NET releases.

## Decision

`net8.0;net10.0`. Both are LTS. Nothing older.

## Consequences

Several things this library depends on are unavailable before .NET 8, and each
of them is load-bearing rather than decorative:

- `INumber<T>` (generic math), which lets one `NumberSchema<T>` cover every
  numeric type instead of one hand-written schema per type.
- `[InlineArray]`, which gives `ParseContext` its allocation-free path buffer.
- `[GeneratedRegex]` with `RegexOptions.NonBacktracking`, which makes the format
  patterns AOT-safe and immune to catastrophic backtracking.
- `IsAotCompatible`, which turns the trim and AOT analyzers into build errors so
  the AOT promise is enforced rather than asserted.
- `MemoryExtensions.ContainsAnyInRange`, used by the code-point fast path.

Supporting `netstandard2.0` would mean either giving these up or maintaining two
implementations behind `#if`, in the hottest code in the library.

`net9.0` is deliberately skipped. It is an STS release, and a consumer targeting
it resolves the `net8.0` asset with no meaningful penalty. Every extra target
framework is another CI axis and another place the conditional code can diverge.

## Trade-offs

.NET Framework 4.8 is excluded outright. That is a real population, but it is not
the population that will adopt a new validation library, and serving it would
cost the performance and AOT characteristics that are the reason to prefer this
library over the established ones.
