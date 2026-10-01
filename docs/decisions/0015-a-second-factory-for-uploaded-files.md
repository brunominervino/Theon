# 15. A second factory, for a type the core cannot know

Status: Accepted

Amends: the glossary entry for *Theo*.

## Context

Every upload endpoint needs the same three rules — a size, a type, a name — and the library had none
of them.

It was not quite true that it had no answer. Checked before building anything: an `IFormFile` can be
validated today with `Theo.Object<IFormFile>().Field(x => x.Length, Theo.Long().Max(...))`, and
`Theo.Collection(...)` accepts an `IFormFileCollection` because it is an `IReadOnlyList<IFormFile>`.
That works. What it produces is a document saying

```json
{"type":"object","properties":{"Length":{},"ContentType":{},"FileName":{}},
 "required":["Length","ContentType","FileName"]}
```

which describes a JSON object with three properties, none of which crosses the wire. By this
repository's own standard — a document that omits a rule is incomplete, one that invents a rule is
wrong — the existing answer is in the second category, which is the worse one. That, rather than
"there is no alternative", is the argument for a schema of its own.

## Where the factory goes

`IFormFile` is an ASP.NET Core type, and the core package has no dependencies and is not getting any.
So a factory method for it cannot sit beside every other factory method, and the glossary sentence
"the static factory every schema is built from" stops being true of `Theo` alone.

**Alternatives.** An extension member on `Theo`, which C# 14 can express: rejected because
`Theo.File()` would read as though it came from the core, and the first thing a reader does with a
name is look for where it lives. A `FormFile.Schema()` entry point: rejected because
`Microsoft.AspNetCore.Http.FormFile` already exists, and any file using both namespaces would get
CS0104 on the name.

**Decision.** A static class named for the subject: `Upload.File()` and `Upload.Files()`, in
`Theon.AspNetCore`. A second factory is a cost, and it is the smallest one available: it is honest
about which package it comes from, it collides with nothing, and the glossary now says that `Theo` is
the factory for everything the core can know about.

## The rules, and what they are worth

`MinSize`, `MaxSize`, `ContentType`, `Extension` and `Named` on a file; `MinCount`, `MaxCount`,
`MaxTotalSize` and `Each` on a set of them.

`MaxTotalSize` is the one addition beyond what was asked for, and it earns its place by being the only
rule that cannot be expressed per file: ten files each under five megabytes come to fifty, and a
budget for the request as a whole is what an upload endpoint actually has.

**The size is the only fact.** `IFormFile.ContentType` is the `Content-Type` header of the multipart
section and `FileName` is its `Content-Disposition`; both are written by whoever sent the request, and
both are a single line to forge. The rules still earn their place — the common failure is somebody
picking the wrong file, not somebody attacking — but a rule a reader believes to be a security control
and which is not is worse than no rule at all, so the type's own documentation says so first and at
length.

**Byte signatures are out of scope, deliberately.** Proving a PNG means reading its first bytes, which
consumes the stream unless the caller rewinds, is I/O on a path otherwise free of it, and needs a table
of magic numbers that is a maintenance obligation belonging to a library whose job that is. The
documented answer is `RefineAsync` over the file, with whatever table the caller trusts.

`ContentType` and `Extension` take no custom message, which matches `UriSchema.Scheme` and is not
merely consistency: an overload taking a message *and* a `params` array cannot be written safely in
C#, because `ContentType("image/png")` then binds the one argument to the message and leaves the set
empty. That was written, and it compiled, and every test using it failed — including the allocation
test, because the schema was silently rejecting every file and the error path allocates. The default
messages already name the acceptable values, which is most of what a custom message would have said.

## How a document says it

OpenAPI 3.1 and JSON Schema 2020-12 spell a binary payload `{"type": "string", "contentMediaType":
"..."}`. `format: binary` is OpenAPI 3.0's spelling, and this library does not serve that dialect;
`contentEncoding` is for content embedded in a JSON string as base64, which a multipart section is not.
So: `contentMediaType`, and `application/octet-stream` when no type was declared — because a document
saying only `string` describes text, and a file is not text.

The size bounds have no keyword at all. `minLength` and `maxLength` count the characters of a string
and a binary section has none, so they are recorded as unrepresentable rather than spelled with a
keyword that means something else. The extension and name rules go the same way: they are about the
section's headers, where no schema keyword reaches.

## Consequences

- `ValidationOrigin.Bytes` and two format names, `content_type` and `file_extension`, are added to the
  core, with sentences in all eight message providers. None of them mentions a file: a size in bytes is
  a quantity like a text length or an element count, and nothing about them pulls ASP.NET Core anywhere
  near the core package. The coverage tests gained a case that builds the error facts by hand, because
  no schema in those test projects can produce one.
- `SchemaDescription.ContentMediaType` is added, which is only usable from another assembly because of
  decision 14. Without that, this package could not have described its own schemas at all, and the
  alternative on offer — `InternalsVisibleTo` — was rejected there for good reasons.
- A valid file and a valid set of files both parse with zero allocations, which is measured in the
  tests rather than asserted: the acceptable values are normalized and joined once when the schema is
  built, and a parse is property reads and span comparisons.
- A failure in one file of a set is reported at that file's index, so a caller rendering a list of
  uploads puts the message on the row it belongs to.

## Trade-offs

Two factories where there was one, and a reader who has learned `Theo` has to learn that files are
somewhere else. The alternative was either a dependency in the core package, which is the one thing
that is never traded, or a name that lies about where it comes from.

`Named` reports at the file's own path rather than under a `FileName` segment. A multipart section is
one field as far as the request is concerned, and whoever has to act on the error picks a different
file; there is no separate field for them to correct. Someone rendering a detailed form may wish the
segment were there.
