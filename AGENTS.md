# AGENTS.md

Guidance for AI agents working in this repository. Read it before changing anything under `src/`.

## What this project is

Theon is a schema definition, validation and parsing library for .NET. It takes its
conceptual lead from Zod but is not a port of it: where a Zod mechanism exists to work around a
gap in TypeScript, the .NET equivalent is usually to delete the mechanism rather than translate it.
`docs/decisions/` records where and why the two diverge, and is the first thing to read before
proposing a design change.

The priority order, when two of these conflict, is: correctness, then developer experience, then
type safety, then a sustainable architecture, then performance, then extensibility, then
integrations.

## Development commands

```bash
dotnet restore Theon.slnx
dotnet build Theon.slnx -c Release
dotnet build eng/Tests.proj -c Release
dotnet format Theon.slnx --verify-no-changes
./artifacts/bin/Theon.Benchmarks/release/Theon.Benchmarks.exe --filter "*" --job short --memory
dotnet pack Theon.slnx -c Release
```

Tests run through the MSBuild `Test` target, not `dotnet test`, and `eng/Tests.proj` gathers every
test project so a new one is picked up by existing. The .NET 10 SDK removed the VSTest
entry point, and the replacement `dotnet test` runner does not complete its handshake with the
Microsoft.Testing.Platform version xunit.v3 ships. The `Test` target runs the test host directly and
covers both target frameworks. If you find `dotnet test` working on a later SDK, change this file in
the same commit that starts relying on it.

Both target frameworks must pass. A change that is green on `net10.0` and red on `net8.0` is red.

## Rules

- **Features without tests are incomplete.** Every rule needs a test that passes, a test that
  fails, and a test for whatever edge case made the rule interesting enough to write.
- **Never weaken a test to make it pass.** If a test fails, either the code is wrong or the test
  encodes a decision nobody wrote down. Find out which before touching either.
- **The core library has zero package references, and keeps them.** `Directory.Packages.props`
  exists for test and benchmark tooling. A dependency in `src/Theon` needs its own decision
  record, not a pull request comment.
- **Warnings are errors.** Do not suppress a diagnostic to get a build green. A suppression needs
  `[SuppressMessage]` with a `Justification` that says why the rule does not apply to that code, in
  that place. There is exactly one such suppression today, on the `Schema` factory.
- **Public means forever.** Prefer `internal`. Every public type and member is a support obligation
  and a binary-compatibility constraint. `EnablePackageValidation` will catch a break against the
  last release, but it cannot catch a bad API that was never necessary.
- **Everything public carries XML documentation**, including parameters. Discoverability through
  IntelliSense is a stated goal, not a nicety: someone should be able to learn most of this library
  by typing `Schema.` and reading.
- **Schemas are immutable.** Every builder method returns a new instance. Never mutate an existing
  schema, never cache mutable per-parse state on one. A schema is built once at start-up and used
  concurrently from every request thread; that promise is tested in
  `ImmutabilityAndConcurrencyTests` and must stay true.
- **Do not commit, tag, push, or publish** unless the human asks in that session. Do not change the
  version number. `VersionPrefix` lives in `src/Directory.Build.props` and is raised in its own
  commit, by a person; `dotnet nuget push` is never run by an agent. See `docs/releasing.md`.

## Never branch on a specific schema type in shared code

No `if (schema is OptionalSchema<T>)`, no list of wrapper type names, no walking a chain of
wrappers looking for a particular one. If shared code needs to know something about a schema,
express it as a structural property that every schema answers, and let each schema answer for
itself.

This is not a style preference. Conditional logic keyed on schema identity is how a parse path
turns into a pile of special cases that only the person who added the last one understands, and
every new schema type then has to be threaded through all of them. A change that adds such a
branch to a parse path will be rejected regardless of how well it is tested.

## The three axes

Any non-trivial change under `src/Theon` has to be weighed on three measurements at once,
and all three reported — including the ones that got worse:

1. **Throughput** — nanoseconds per parse, from `benchmarks/`.
2. **Allocations** — bytes per parse. **A valid value must allocate zero bytes.** That holds today
   for strings, numbers, and flat and nested objects, and it is the single property most likely to
   be lost by accident. A change that allocates on the success path needs an explicit reason.
3. **Public API surface** — how many new public types and members. A feature that adds five public
   types to save one line at a call site is not a good trade.

Benchmark honestly. Use `--job short` while iterating, but never quote a short-job number as a
result: its standard deviation is frequently larger than the effect being measured. Re-run the
affected benchmarks with the default job before reporting, and check the machine is not otherwise
busy — a loaded machine will invent a double-digit percentage difference between two identical
builds.

## Format validators: matching the specification is not the goal

"RFC 5322 permits this address, so `Email()` must accept it" is not an argument, and a pull request
that widens a format pattern on that basis alone will be closed.

A format rule exists to catch the mistake a person made while filling in a form. An address that is
legal by the specification but that no mail provider would ever issue is still a typo, and widening
the pattern to admit it makes the rule worse at its actual job while costing every caller. The
escape hatch already exists and is documented: `Matches()` with your own pattern, or `Refine()`.

Patterns that see text from strangers must be `[GeneratedRegex]` and should be
`RegexOptions.NonBacktracking`. Linear-time matching by construction is how this library avoids
regular-expression denial of service, rather than trying to outrun it with a timeout.

## Things agents must not do

- Do not add a package reference to `src/Theon`.
- Do not introduce reflection, `Reflection.Emit`, `Expression.Compile`, or dynamic code generation
  anywhere in `src/`. `IsAotCompatible` will turn most of these into build errors; the point is not
  to look for a way around it.
- Do not disable an analyzer repository-wide to fix one call site.
- Do not change `docs/decisions/` to match new code. If the code contradicts a decision record,
  that is the thing to raise, not to paper over. Superseding a decision means a new record that
  says what changed and why.
- Do not reproduce code from the Zod repository. Concepts and hard-won semantics are exactly what we
  want to learn from; implementation is to be written here. Where a specific behaviour was adopted
  because Zod arrived at it after real-world feedback, say so in a comment or a decision record.
- Do not add an `Optional()` method. See `docs/decisions/0004-nullability.md`.
- Do not rename the package, the namespace or the `Theo` factory. The name is settled and is
  personal to the maintainer; `SchemaSharp` was the working title and was abandoned because the id
  is taken on nuget.org by an unrelated project.
- Do not rename the factory to `T`. It does not compile: inside any generic whose type parameter is
  named `T` — which is the universal convention — the type parameter shadows it and the call fails
  with CS0704.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

A `status:`-prefixed vocabulary (`status: needs-triage`, `status: needs-info`, `status: ready`, `status: needs-maintainer`) plus GitHub's default `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and one `docs/decisions/` at the repo root. See `docs/agents/domain.md`.
