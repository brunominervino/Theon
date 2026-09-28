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
| `dotnet pack ... -c Release` | `0.1.0-preview.1` |
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
dotnet add package Theon --version 0.1.0-preview.1
dotnet run
```

Check that IntelliSense shows the XML documentation. If it does not, the documentation file did not
make it into the package, and most of the discoverability this library is designed around is gone.

## API compatibility

`EnablePackageValidation` is on. Today it checks the package is internally consistent across target
frameworks. Once a version has been published, set `PackageValidationBaselineVersion` to it in
`src/Directory.Build.props`, and the build will then fail on any change that breaks binary
compatibility against that release.

Do that with the first published version. It is the cheapest guard available against accidentally
shipping a breaking change, and it only works if somebody remembers to turn it on.

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

```bash
dotnet nuget push artifacts/package/release/Theon.<version>.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key <key>
```

The `.snupkg` is pushed automatically alongside the `.nupkg` by the same command.

Before pushing, confirm: the full test suite passes on both target frameworks, `dotnet format
--verify-no-changes` is clean, the package has been installed and run from a local feed, and
`VersionPrefix` is what you intend to publish.
