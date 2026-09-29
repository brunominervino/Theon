# Packaging and releasing

## Producing a package

```bash
dotnet pack src/Theon/Theon.csproj -c Release
```

Output lands in `artifacts/package/release/`: a `.nupkg` with the library and a `.snupkg` with the
debug symbols. Everything the package needs is already configured in `src/Directory.Build.props` —
there is no separate `.nuspec` to keep in step.

The package contains `lib/net8.0` and `lib/net10.0`, the XML documentation for both, the README, and
no dependencies at all.

## Versions

`VersionPrefix` lives in `src/Directory.Build.props` and is the only place a version number is
written. Packing produces a prerelease unless told otherwise:

| Command | Version produced |
|---|---|
| `dotnet pack ... -c Release` | `0.1.0-preview.5` |
| `dotnet pack ... -c Release -p:VersionSuffix=` | `0.1.0` |
| `dotnet pack ... -c Release -p:VersionSuffix=rc.1` | `0.1.0-rc.1` |
| `dotnet pack ... -c Release -p:Version=0.2.0` | `0.2.0`, overriding both |

The default is a prerelease on purpose. `1.0.0` on NuGet is a promise that the public API is stable,
and this one is not yet; a package that makes that promise early cannot take it back.

Raising `VersionPrefix` is its own commit, containing nothing else.

## Verifying a package before publishing

Never publish a package that has not been installed from a feed and run. Building it proves it
compiles; installing it proves the parts a consumer actually sees are present and correct.

```bash
mkdir /tmp/pkgtest && cd /tmp/pkgtest
dotnet new console --name PkgTest --output .
```

Add a `nuget.config` pointing at the local output:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/path/to/Theon/artifacts/package/release" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

Then install it, write a few lines against the public API, and run them:

```bash
dotnet add package Theon --version 0.1.0-preview.5
dotnet run
```

Check that IntelliSense shows the XML documentation. If it does not, the documentation file did not
make it into the package, and most of the discoverability this library is designed around is gone.

## API compatibility

`EnablePackageValidation` is on, and `PackageValidationBaselineVersion` names the last published
release. Packing downloads that package and compares the public API against it, failing the build
on anything that breaks binary compatibility — a removed member, a narrowed accessibility, a
changed signature.

Raise the baseline as part of releasing, alongside `VersionSuffix`. Each version is then checked
against the one immediately before it, rather than against an ever more distant ancestor that
eventually makes every legitimate change look like a break.

It is worth confirming the check is live rather than trusting that it is, because a silently
skipped validation looks exactly like a passing one. Make a public member `internal`, pack, and
expect `CP0002`; then put it back.

## The package id

The id is claimed on nuget.org by whoever publishes it first, and it cannot be taken back
afterwards. Check that the id is free **before** building anything on the name:

```bash
dotnet package search <name> --exact-match
```

A free id is worth claiming early with a prerelease, even if the library is months from useful.
That costs nothing and removes the risk of having to rename a published project later.

## Publishing

Publishing is a deliberate, human-initiated act. It is not wired to a push or a tag.

This is not caution for its own sake: a stolen push credential that can cut a tag can publish a
package if a tag is what triggers publishing. Requiring a manual dispatch means a stolen credential
alone is not enough, and a version bump landing on `main` publishes nothing by itself.

### How it is set up

The `release` workflow publishes through **NuGet Trusted Publishing**, so there is no long-lived
API key anywhere — not in the repository secrets, not on a developer machine. GitHub issues a
signed OIDC token describing the repository, the workflow file and the environment; nuget.org
checks it against a registered policy and hands back a key valid for one hour. There is no
publishing secret to leak, rotate, or accidentally print into a log.

The policy on nuget.org must match the workflow exactly:

| Policy field | Value |
| --- | --- |
| Repository Owner | `brunominervino` |
| Repository | `Theon` |
| Workflow File | `release.yml` (file name only, no path) |
| Environment | `nuget` |
| Scopes | Push, "new packages and package versions" |
| Glob pattern | `Theon*` |

`Theon*` rather than `*`, so this repository can publish the package and its future satellites and
nothing else in the account.

### Keeping the version in one place

`VersionSuffix` in `src/Directory.Build.props` is what an empty Version field publishes, so it has
to name the **next** version, not the last one. Bump it in its own commit as part of releasing.

Typing the version into the workflow field instead works, and is the right escape hatch for
republishing or for cutting an `rc` out of sequence. It is not the routine path: the file then
records a version that has already shipped, and nobody reading the repository can tell what comes
next. That drift is what left the file claiming `preview.1` while `preview.4` was live.

The GitHub side needs an environment named `nuget` with at least one required reviewer. That is
what makes the job wait for a person, and nuget.org independently verifies the environment claim,
so a run that skipped the gate cannot obtain a key either.

### Running it

Actions → release → Run workflow. Leave **dry run** ticked to build and inspect the package without
publishing; untick it to publish for real. The workflow refuses to run from any branch but `main`,
and runs the full test suite and the format check before it packs anything.

Before publishing, confirm: the tests pass on both target frameworks, the format check is clean, the
package has been installed and run from a local feed, and `VersionPrefix` is what you intend to
publish.

### A note on the first publish

A policy for a private repository starts out *temporarily active* for seven days. nuget.org needs
the GitHub repository and owner IDs to pin the policy to this exact repository, and it only learns
them from a successful publish. Publish within that window and the policy becomes permanent; let it
lapse and you can restart the window at any time.
