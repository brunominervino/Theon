# Changelog

Every published version, newest first. Each entry says what changed and, where something broke, what
to do about it.

The version is a prerelease on purpose. `1.0.0` on NuGet is a promise that the public API is stable,
and this one is not yet — so a `preview` may break a signature where a stable release could not. Every
such break is listed below with its migration.

All nine packages version in lockstep: `Theon`, `Theon.AspNetCore`, and `Theon.Localization` in
`PtBr`, `Es`, `Fr`, `De`, `It`, `Nl` and `Pl`.

## 0.1.0-preview.9

Nine gaps found by an application using preview.8 in production, each of which it had worked around
with code of its own.

### Added

- **`Theo.Boolean()`.** There was no `Schema<bool>`, so a boolean property could not be bound to a
  field — and an object holding one therefore had no entry for it in any generated document. There
  is no `IsTrue`: demanding one particular value is `Theo.Literal(true)`, which already writes
  `const: true`.
- **An error code of the caller's own.** `ValidationErrorInfo.CustomCode` and
  `ValidationError.CustomCode` carry a string the caller chose — `"cnpj.digito_verificador"` — beside
  the library's own enum. One `Refine` overload sets it:

  ```csharp
  schema.Refine(IsValidCnpj, "Informe um CNPJ válido.", "cnpj.digito_verificador");
  schema.Refine(IsValidCnpj, message: null, code: "cnpj.digito_verificador");  // the provider says it
  ```

  The enum still reports `Custom`, so nothing switching on it changes. A message given on the rule
  still wins over every provider; passing `null` hands the sentence to the provider chain, which is
  how a localization package says one particular rule in its own language.
- **`MessageProviders.Chain(a, b, …)`.** The first provider to return a message wins, and one
  returning `null` hands on. The shape this is for:

  ```csharp
  SchemaGlobalOptions.MessageProvider = MessageProviders.Chain(
      NucleoMessages.Provider,                 // names this application's own rules
      BrazilianPortugueseMessages.Provider);   // says everything else in Portuguese
  ```

- **A naming policy for paths and properties.** `ParseOptions.PropertyNamingPolicy` and
  `SchemaGlobalOptions.PropertyNamingPolicy` rename an error path;
  `JsonSchemaOptions.PropertyNamingPolicy` renames the keys of `properties` and the entries of
  `required`. `null` everywhere by default, which is the C# member name and what preview.8 produced.
  An index is a position rather than a name, so nothing happens to it, and a map's keys are values
  rather than members, so nothing happens to them either.
- **`JsonSchemaOptions.EnumMemberNamingPolicy`**, separate from the property policy on purpose:
  `System.Text.Json` configures the two independently.
- **`TheonValidationOptions`, `AddTheon` and `Validate(schema, configure)`** in `Theon.AspNetCore`.
  The status, the problem document's `type`, `title` and a `code` member, whether each error carries
  its own code, and the policy the error paths are named with:

  ```csharp
  builder.Services.AddTheon(options =>
  {
      options.Status = StatusCodes.Status422UnprocessableEntity;
      options.Title = "Falha de validação";
      options.Code = "validacao";
      options.IncludeErrorCodes = true;
      options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
  });
  ```

- **`TheonSchemaMetadata.For(schema)`.** The constructor was internal, so only this package could put
  a schema on an endpoint. A filter somebody else wrote can now publish the same metadata, and
  whatever produces the OpenAPI description cannot tell the two apart.
- **A `Describe` overload taking `JsonSchemaOptions`**, so a generator of your own can ask for a
  direction, a reference prefix and both naming policies in one object.

### Changed

- **A format name the caller chose now reaches the document.** `Matches(pattern, "cnpj")` wrote only
  the pattern and dropped the name; it now writes `format: "cnpj"` *and* the pattern. 2020-12 puts
  `format` in an annotation vocabulary and requires an unrecognised value to be ignored rather than
  to fail, so a name of your own is safe there by specification. Names this library chose for its own
  rules are unchanged: `format: "lowercase"` says nothing to any tool, and `base64` has a spelling of
  its own in this dialect that is not `format`.
- **An enum publishes the names it serializes under.** `[JsonStringEnumMemberName("editar")]` is
  read, so `enum` lists `editar` where the document used to list `Editar`. A member that named itself
  keeps that name; members that said nothing take `EnumMemberNamingPolicy`.
- **The Portuguese provider says a caller's format name** instead of falling through to English, which
  used to produce a response in two languages. The same one-line gap remains in the other seven
  language packages.

### Breaking

One behavioural break, in the generated document. No signature changed.

- **A rule written after a normalization is no longer claimed of the input.** `Trim().MaxLength(14)`
  accepts fifteen characters where one is a space, and `Trim().Email()` accepts
  `"  ada@example.com  "` — so a document claiming `maxLength: 14` or `format: email` of the *input*
  described a stricter server than the real one, and a client generated from it refused values the
  endpoint takes. Those claims are now omitted, and recorded so that
  `UnrepresentablePolicy.Throw` names them.

  On the output side it is the rules *before* the normalization that go, for the mirror reason:
  `MinLength(3).Trim()` accepts `"  a  "` and produces a single character.

  **Migration.** Where you want the full constraints — a response body, or a request document whose
  clients you know tolerate the untrimmed form — ask for the output side:

  ```csharp
  schema.ToJsonSchema(new JsonSchemaOptions { Direction = DescriptionDirection.Output });
  ```

  Where you want one keyword back on the input side, `Amend` puts it there:

  ```csharp
  schema.ToJsonSchema(new JsonSchemaOptions
  {
      Amend = node =>
      {
          if (node.Path == "Email")
          {
              node.Json["format"] = "email";
              node.Expressed = true;
          }
      },
  });
  ```

  Dropped rather than narrowed, and uniformly rather than cleverly: `Trim` only shortens, so a lower
  bound written after it does still hold of the input and is being given up — but `Normalize` takes an
  arbitrary function and guarantees nothing, so no general rule exists, and making the document's
  completeness depend on which of two interchangeable spellings the author reached for is worse than
  being uniformly incomplete.

### Public surface

51 → 62 types and 300 → 400 members across the three releases since preview.7, each raise recorded
beside the constant in `GuardrailTests`. The assembly ceiling went 160 → 192 KiB in preview.8.

## 0.1.0-preview.8

- **A description has a direction.** `JsonSchemaOptions.Direction` chooses the input side or the
  output side. The generator described the input and only the input, which is right for a request
  body and wrong for a response body. `Input` remains the default. See decision 12.
- **An amendment for what a document cannot say.** `JsonSchemaOptions.Amend` receives every node once
  it is written, with its path and the rules it could not express, and may declare that it expressed
  them. See decision 13.
- **The description model is public.** `SchemaDescription`, `SchemaKind`, `PropertyDescription`,
  `DescriptionContext` and `Schema<,>.Describe`, so a schema from another assembly can describe
  itself and a generator for another format can be written at all. The model is immutable. See
  decision 14.
- **Uploaded files have a factory.** `Upload.File()` and `Upload.Files()` in `Theon.AspNetCore`, with
  size, content type, extension and a schema over the file name. The content type and the file name
  are claims and not facts, which the documentation says at length. See decision 15.
- **A document becomes a schema.** `Theo.JsonSchema(document)` reads a 2020-12 document and validates
  a `JsonNode` against it, for checking a payload against a description somebody else published. See
  decision 16.
- **`Iso8601Time()` and `Iso8601Duration()`.**

## 0.1.0-preview.7

- String formats: `Email`, `Url`, `Uuid`, `Base64`, `Base64Url`, `Hex`, `E164`, `Iso8601`,
  `Iso8601Date`, `Ipv4`, `Ipv6`, `Cidr`, `Hostname`, `Jwt`, `CreditCard`, `Iban`, `Matches`,
  `Lowercase`, `Uppercase`, `StartsWith`, `EndsWith`, `Contains`.
- Discriminated unions: `Theo.Subtypes<TBase>()`.
- Generated documents: `ToJsonSchema()` and `ToJsonSchemaDocument()`, JSON Schema 2020-12, with
  `JsonSchemaOptions`. See decision 9.
- Three more languages: German, Italian, Dutch, Polish.
- `Transform` gained an overload that validates its result, and `TryTransform` arrived for the
  conversion that can fail. See decision 10.
- `Refine` gained the form that receives the parse and reports for itself. See decision 11.

## 0.1.0-preview.6

- Recursive schemas: `Theo.Lazy<T>()`.
- Dictionaries: `Theo.Record<K, V>()`.
- Alternatives: `Theo.OneOf<T>()` and `Theo.Literal<T>()`.
- Brazilian Portuguese messages.

## 0.1.0-preview.5

- `Theon.AspNetCore`: `.Validate(schema)` on an endpoint and on a group, answering
  `ValidationProblemDetails`, and `TheonSchemaMetadata` on the endpoint for a document generator to
  find.

## 0.1.0-preview.4

- Errors reshaped into the forms callers render: `Flatten()`, `ToTree()`, `ToPrettyString()`. See
  decision 5.
- Asynchronous validation as a second path: `RefineAsync`, `ParseAsync`, `SafeParseAsync`. See
  decision 8.

## 0.1.0-preview.3

- Conditional rules: `When()` in two forms, and `Required()`.
- `ObjectSchema` accepts value types, so a struct and a record struct parse like a class.
- Binary compatibility is checked against the last published release.

## 0.1.0-preview.2

- `Theo.Collection<T>()`, `Theo.Enum<T>()`, and the temporal schemas: `DateTime`, `DateTimeOffset`,
  `DateOnly`, `TimeOnly`.
- Publishing moved to NuGet trusted publishing, so there is no long-lived API key anywhere.

## 0.1.0-preview.1

- The core: `Theo.String()`, the numeric schemas, `Theo.Object<T>()`, `Theo.Guid()`,
  `Theo.TimeSpan()`, `Theo.Uri()`, `Theo.Set<T>()`, the error model, and the message provider chain.
