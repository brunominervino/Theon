using Microsoft.AspNetCore.Http;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.AspNetCore;

/// <summary>Rules for one uploaded file.</summary>
/// <remarks>
/// <para>
/// Built from <see cref="Upload.File"/>. Immutable, like every schema here: each method returns a new
/// instance.
/// </para>
/// <para>
/// <strong>The content type and the file name are claims, not facts.</strong>
/// <see cref="IFormFile.ContentType"/> comes from the <c>Content-Type</c> header of the multipart
/// section, and <see cref="IFormFile.FileName"/> from its <c>Content-Disposition</c> — both written by
/// whoever sent the request, and both trivially forged. <see cref="ContentType"/> and
/// <see cref="Extension"/> catch the honest mistake, which is the common one; they
/// are not a security control, and treating them as one is worse than having neither, because it is a
/// check a reader believes in.
/// </para>
/// <para>
/// What a file actually is can only be learned from its bytes, and this library deliberately does not
/// look at them: reading the stream consumes it unless the caller rewinds, it is I/O on a path that is
/// otherwise free of it, and a table of magic numbers is a maintenance obligation that belongs in a
/// library whose job that is. Where the real type matters, use <c>RefineAsync</c> over the file and
/// read the opening bytes yourself, with whatever table you trust.
/// </para>
/// <para>
/// Only the size is a fact, and a size the server measured.
/// </para>
/// </remarks>
public sealed class FormFileSchema : Schema<IFormFile>
{
    private readonly long? _minSize;
    private readonly long? _maxSize;
    private readonly string[]? _contentTypes;
    private readonly string[]? _extensions;
    private readonly Schema<string>? _name;
    private readonly string? _minSizeMessage;
    private readonly string? _maxSizeMessage;

    // The acceptable values as one sentence fragment, joined when the schema is built rather than when
    // a file fails. The error path has already decided to allocate, but it may run on every file of a
    // large upload, and this costs nothing to do once.
    private readonly string? _contentTypesText;
    private readonly string? _extensionsText;

    internal FormFileSchema()
    {
    }

    private FormFileSchema(
        long? minSize,
        long? maxSize,
        string[]? contentTypes,
        string[]? extensions,
        Schema<string>? name,
        string? minSizeMessage,
        string? maxSizeMessage)
    {
        _minSize = minSize;
        _maxSize = maxSize;
        _contentTypes = contentTypes;
        _extensions = extensions;
        _name = name;
        _minSizeMessage = minSizeMessage;
        _maxSizeMessage = maxSizeMessage;
        _contentTypesText = contentTypes is null ? null : string.Join(", ", contentTypes);
        _extensionsText = extensions is null ? null : string.Join(", ", extensions);
    }

    /// <summary>Requires the file to be at least <paramref name="bytes"/> long.</summary>
    /// <param name="bytes">The smallest acceptable size, in bytes.</param>
    /// <param name="message">A message to report instead of the default.</param>
    /// <remarks>
    /// Mostly for rejecting the empty file, which is what a browser sends when somebody picks nothing
    /// and submits anyway.
    /// </remarks>
    public FormFileSchema MinSize(long bytes, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        return With(minSize: bytes, minSizeMessage: message);
    }

    /// <summary>Requires the file to be at most <paramref name="bytes"/> long.</summary>
    /// <param name="bytes">The largest acceptable size, in bytes.</param>
    /// <param name="message">A message to report instead of the default.</param>
    /// <remarks>
    /// <para>
    /// The default message names the bound in bytes, because that is the bound the schema declared and
    /// this library never reports a number it was not given. A sentence a person would rather read —
    /// "no larger than 5 MB" — is one line of <paramref name="message"/>.
    /// </para>
    /// <para>
    /// This runs after the request body has already been read. It is a rule about what the application
    /// accepts, not a defence against a large upload: that belongs to the server's own request limits,
    /// which refuse the body before it arrives.
    /// </para>
    /// </remarks>
    public FormFileSchema MaxSize(long bytes, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        return With(maxSize: bytes, maxSizeMessage: message);
    }

    /// <summary>Requires the declared content type to be one of <paramref name="contentTypes"/>.</summary>
    /// <param name="contentTypes">The acceptable media types, such as <c>image/png</c>.</param>
    /// <remarks>
    /// <para>
    /// Compared case-insensitively, which is what the specification says of a media type, and compared
    /// against the media type alone: a section that declares <c>text/csv; charset=utf-8</c> matches
    /// <c>text/csv</c>, because the parameter is not part of what was claimed.
    /// </para>
    /// <para>
    /// The value compared is the client's claim. See the remarks on <see cref="FormFileSchema"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="contentTypes"/> was empty.</exception>
    public FormFileSchema ContentType(params string[] contentTypes)
    {
        ArgumentNullException.ThrowIfNull(contentTypes);

        if (contentTypes.Length == 0)
        {
            throw new ArgumentException(
                "Name at least one content type. A rule that accepts nothing rejects every file, " +
                "which is never what anyone meant to write.",
                nameof(contentTypes));
        }

        // Copied, so that a caller who keeps the array and changes it afterwards cannot change this
        // schema. The same reasoning as UriSchema.Scheme.
        return With(contentTypes: (string[])contentTypes.Clone());
    }

    /// <summary>Requires the file name to end with one of <paramref name="extensions"/>.</summary>
    /// <param name="extensions">
    /// The acceptable extensions, with or without the leading dot: <c>.png</c> and <c>png</c> mean the
    /// same thing.
    /// </param>
    /// <remarks>
    /// <para>
    /// Compared case-insensitively, because a file called <c>PHOTO.PNG</c> is a PNG.
    /// </para>
    /// <para>
    /// The name compared is the client's claim. See the remarks on <see cref="FormFileSchema"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="extensions"/> was empty.</exception>
    public FormFileSchema Extension(params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        if (extensions.Length == 0)
        {
            throw new ArgumentException(
                "Name at least one extension. A rule that accepts nothing rejects every file, which " +
                "is never what anyone meant to write.",
                nameof(extensions));
        }

        var normalized = new string[extensions.Length];

        for (var i = 0; i < extensions.Length; i++)
        {
            var extension = extensions[i];
            ArgumentException.ThrowIfNullOrWhiteSpace(extension);

            // Normalized once, while the schema is being built, so that a parse is a span comparison
            // and nothing else.
            normalized[i] = extension[0] == '.' ? extension : "." + extension;
        }

        return With(extensions: normalized);
    }

    /// <summary>Applies <paramref name="schema"/> to the file name.</summary>
    /// <param name="schema">The schema the file name must satisfy.</param>
    /// <remarks>
    /// <para>
    /// For whatever <see cref="Extension"/> does not cover: a length limit, a pattern,
    /// a refusal of a name containing a path separator. Every string rule in the library composes here.
    /// </para>
    /// <para>
    /// Failures are reported at the file's own path rather than under a <c>FileName</c> segment. A
    /// multipart section is one field as far as the request is concerned, and whoever has to act on the
    /// error picks a different file; there is no separate field for them to correct.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Upload.File().Named(Theo.String().MaxLength(100).Matches(SafeFileName));
    /// </code>
    /// </example>
    public FormFileSchema Named(Schema<string> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return With(name: schema);
    }

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, IFormFile input, out IFormFile output)
    {
        output = input;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "file",
                Received = "null",
            });

            return false;
        }

        var errorsBefore = context.ErrorCount;

        if (TooSmall(input.Length) is { } small)
        {
            context.AddError(small, _minSizeMessage);
        }

        if (TooBig(input.Length) is { } big)
        {
            context.AddError(big, _maxSizeMessage);
        }

        if (WrongContentType(input.ContentType) is { } type)
        {
            context.AddError(type);
        }

        if (WrongExtension(input.FileName) is { } extension)
        {
            context.AddError(extension);
        }

        // Last, because a name rule is the one that can run arbitrary code, and a file that already
        // failed on size has nothing to gain from it.
        _name?.TryParse(ref context, input.FileName, out _);

        return context.ErrorCount == errorsBefore;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Overridden so that asynchrony reaches the schema given to <see cref="Named"/>. Without this the
    /// rules here would run, and an asynchronous rule inside that schema would refuse to run at all.
    /// </remarks>
    public override async ValueTask<ParseOutcome<IFormFile>> TryParseAsync(
        AsyncParseContext context,
        IFormFile input)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "file",
                Received = "null",
            });

            return new ParseOutcome<IFormFile>(false, null!);
        }

        var errorsBefore = context.ErrorCount;

        if (TooSmall(input.Length) is { } small)
        {
            context.AddError(small, _minSizeMessage);
        }

        if (TooBig(input.Length) is { } big)
        {
            context.AddError(big, _maxSizeMessage);
        }

        if (WrongContentType(input.ContentType) is { } type)
        {
            context.AddError(type);
        }

        if (WrongExtension(input.FileName) is { } extension)
        {
            context.AddError(extension);
        }

        if (_name is not null)
        {
            await _name.TryParseAsync(context, input.FileName).ConfigureAwait(false);
        }

        return new ParseOutcome<IFormFile>(context.ErrorCount == errorsBefore, input);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// A multipart file is a string carrying content, which 2020-12 spells <c>contentMediaType</c> and
    /// which is what OpenAPI 3.1 took for the same job. <c>format: binary</c> is OpenAPI 3.0's spelling
    /// and this library does not serve that dialect; <c>contentEncoding</c> is for content embedded in
    /// a JSON string as base64, which a multipart section is not.
    /// </para>
    /// <para>
    /// The size bounds have no keyword at all. <c>minLength</c> and <c>maxLength</c> count the
    /// characters of a string, and a binary section has none, so claiming them would make the document
    /// wrong rather than incomplete. They are recorded as unrepresentable instead, along with the
    /// extension and name rules, which live in the section's headers where no schema keyword reaches.
    /// </para>
    /// </remarks>
    public override SchemaDescription Describe(DescriptionContext context)
    {
        List<string>? lost = null;

        if (_minSize is not null || _maxSize is not null)
        {
            (lost ??= []).Add("a file size bound, which has no keyword for content in this dialect");
        }

        if (_extensions is not null)
        {
            (lost ??= []).Add("a file extension rule, which is about a header and not about content");
        }

        if (_name is not null)
        {
            (lost ??= []).Add("a rule on the file name, which is about a header and not about content");
        }

        // Several acceptable types are a union of one-type descriptions, because contentMediaType takes
        // one value. One type, or none, is a single node.
        if (_contentTypes is { Length: > 1 })
        {
            var branches = new List<SchemaDescription>(_contentTypes.Length);

            foreach (var contentType in _contentTypes)
            {
                branches.Add(new SchemaDescription { ContentMediaType = contentType });
            }

            return new SchemaDescription
            {
                Kind = SchemaKind.String,
                AnyOf = branches,
                Unrepresentable = lost,
            };
        }

        return new SchemaDescription
        {
            Kind = SchemaKind.String,

            // Nothing declared means any bytes at all, which is what application/octet-stream says. It
            // is the honest answer and not a guess: the alternative is a document saying "string",
            // which describes text, and a file is not text.
            ContentMediaType = _contentTypes is { Length: 1 }
                ? _contentTypes[0]
                : "application/octet-stream",
            Unrepresentable = lost,
        };
    }

    // The rules as decisions rather than as reports.
    //
    // Both paths need them and neither can share a context with the other: ParseContext is a ref struct
    // and cannot cross an await, which is the trade decision 8 made. So what is shared is the judgement
    // -- whether this file fails and with which facts -- and each path reports it through the context it
    // has. Nothing here allocates unless a file actually fails, because a ValidationErrorInfo is a
    // struct and so is the nullable wrapping it.
    private ValidationErrorInfo? TooSmall(long length) =>
        _minSize is { } minimum && length < minimum
            ? new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Bytes,
                Minimum = minimum,
                Inclusive = true,
            }
            : null;

    private ValidationErrorInfo? TooBig(long length) =>
        _maxSize is { } maximum && length > maximum
            ? new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Bytes,
                Maximum = maximum,
                Inclusive = true,
            }
            : null;

    private ValidationErrorInfo? WrongContentType(string? declared)
    {
        if (_contentTypes is not { } acceptable)
        {
            return null;
        }

        // The parameter is not part of what was claimed: "text/csv; charset=utf-8" claims text/csv.
        var media = declared.AsSpan();
        var parameter = media.IndexOf(';');

        if (parameter >= 0)
        {
            media = media[..parameter];
        }

        media = media.Trim();

        foreach (var candidate in acceptable)
        {
            if (media.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new ValidationErrorInfo
        {
            Code = ValidationErrorCode.InvalidFormat,
            Format = "content_type",
            Expected = _contentTypesText,
        };
    }

    private ValidationErrorInfo? WrongExtension(string? fileName)
    {
        if (_extensions is not { } acceptable)
        {
            return null;
        }

        var name = fileName.AsSpan();

        foreach (var candidate in acceptable)
        {
            if (name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new ValidationErrorInfo
        {
            Code = ValidationErrorCode.InvalidFormat,
            Format = "file_extension",
            Expected = _extensionsText,
        };
    }

    private FormFileSchema With(
        long? minSize = null,
        long? maxSize = null,
        string[]? contentTypes = null,
        string[]? extensions = null,
        Schema<string>? name = null,
        string? minSizeMessage = null,
        string? maxSizeMessage = null) =>
        new(
            minSize ?? _minSize,
            maxSize ?? _maxSize,
            contentTypes ?? _contentTypes,
            extensions ?? _extensions,
            name ?? _name,
            minSizeMessage ?? _minSizeMessage,
            maxSizeMessage ?? _maxSizeMessage);
}
