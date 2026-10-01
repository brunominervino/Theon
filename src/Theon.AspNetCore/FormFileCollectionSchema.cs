using Microsoft.AspNetCore.Http;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.AspNetCore;

/// <summary>Rules for the set of files in one request.</summary>
/// <remarks>
/// <para>
/// Built from <see cref="Upload.Files"/>. Immutable, like every schema here.
/// </para>
/// <para>
/// A failure in one file is reported at that file's index, so a caller rendering a list of uploads can
/// put the message against the row it belongs to.
/// </para>
/// </remarks>
public sealed class FormFileCollectionSchema : Schema<IFormFileCollection>
{
    private readonly int? _minCount;
    private readonly int? _maxCount;
    private readonly long? _maxTotalSize;
    private readonly Schema<IFormFile>? _each;
    private readonly string? _minCountMessage;
    private readonly string? _maxCountMessage;
    private readonly string? _maxTotalSizeMessage;

    internal FormFileCollectionSchema()
    {
    }

    private FormFileCollectionSchema(
        int? minCount,
        int? maxCount,
        long? maxTotalSize,
        Schema<IFormFile>? each,
        string? minCountMessage,
        string? maxCountMessage,
        string? maxTotalSizeMessage)
    {
        _minCount = minCount;
        _maxCount = maxCount;
        _maxTotalSize = maxTotalSize;
        _each = each;
        _minCountMessage = minCountMessage;
        _maxCountMessage = maxCountMessage;
        _maxTotalSizeMessage = maxTotalSizeMessage;
    }

    /// <summary>Requires at least <paramref name="count"/> files.</summary>
    /// <param name="count">The fewest acceptable files.</param>
    /// <param name="message">A message to report instead of the default.</param>
    public FormFileCollectionSchema MinCount(int count, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return With(minCount: count, minCountMessage: message);
    }

    /// <summary>Requires at most <paramref name="count"/> files.</summary>
    /// <param name="count">The most acceptable files.</param>
    /// <param name="message">A message to report instead of the default.</param>
    public FormFileCollectionSchema MaxCount(int count, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return With(maxCount: count, maxCountMessage: message);
    }

    /// <summary>Requires the files to come to at most <paramref name="bytes"/> between them.</summary>
    /// <param name="bytes">The largest acceptable total, in bytes.</param>
    /// <param name="message">A message to report instead of the default.</param>
    /// <remarks>
    /// <para>
    /// The one rule that only exists at this level. Ten files of five megabytes each satisfy every
    /// per-file rule and still come to fifty, and a budget for the request as a whole is what an upload
    /// endpoint usually has.
    /// </para>
    /// <para>
    /// Reported against the set rather than against any one file, because no single file is the one at
    /// fault.
    /// </para>
    /// </remarks>
    public FormFileCollectionSchema MaxTotalSize(long bytes, string? message = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        return With(maxTotalSize: bytes, maxTotalSizeMessage: message);
    }

    /// <summary>Applies <paramref name="schema"/> to every file.</summary>
    /// <param name="schema">The schema each file must satisfy.</param>
    /// <example>
    /// <code>
    /// Upload.Files()
    ///     .MinCount(1)
    ///     .MaxCount(10)
    ///     .MaxTotalSize(20 * 1024 * 1024)
    ///     .Each(Upload.File().ContentType("application/pdf"));
    /// </code>
    /// </example>
    public FormFileCollectionSchema Each(Schema<IFormFile> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return With(each: schema);
    }

    /// <inheritdoc />
    public override bool TryParse(
        ref ParseContext context,
        IFormFileCollection input,
        out IFormFileCollection output)
    {
        output = input;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "files",
                Received = "null",
            });

            return false;
        }

        var errorsBefore = context.ErrorCount;

        CheckCount(ref context, input.Count);

        // By index rather than by foreach. IFormFileCollection is an IReadOnlyList, so the indexer is
        // there, and taking the enumerator through the interface would box it -- which is the one
        // documented place this library allocates on a valid value, and not a precedent worth growing.
        long total = 0;

        for (var i = 0; i < input.Count; i++)
        {
            var file = input[i];
            total += file?.Length ?? 0;

            if (_each is not null)
            {
                context.PushIndex(i);
                _each.TryParse(ref context, file!, out _);
                context.Pop();
            }

            if (context.ShouldStop)
            {
                break;
            }
        }

        if (_maxTotalSize is { } budget && total > budget)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Bytes,
                    Maximum = budget,
                    Inclusive = true,
                },
                _maxTotalSizeMessage);
        }

        return context.ErrorCount == errorsBefore;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Overridden so that asynchrony reaches the schema given to <see cref="Each"/> — a rule that reads
    /// the opening bytes of each file, for instance, which is the documented way to learn what a file
    /// actually is. Files are awaited one at a time, which is the order every composite schema here
    /// uses: running them together would turn one slow check per request into several concurrent ones.
    /// </remarks>
    public override async ValueTask<ParseOutcome<IFormFileCollection>> TryParseAsync(
        AsyncParseContext context,
        IFormFileCollection input)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "files",
                Received = "null",
            });

            return new ParseOutcome<IFormFileCollection>(false, null!);
        }

        var errorsBefore = context.ErrorCount;

        CheckCountAsync(context, input.Count);

        long total = 0;

        for (var i = 0; i < input.Count; i++)
        {
            var file = input[i];
            total += file?.Length ?? 0;

            if (_each is not null)
            {
                context.PushIndex(i);
                await _each.TryParseAsync(context, file!).ConfigureAwait(false);
                context.Pop();
            }

            if (context.ShouldStop)
            {
                break;
            }
        }

        if (_maxTotalSize is { } budget && total > budget)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Bytes,
                    Maximum = budget,
                    Inclusive = true,
                },
                _maxTotalSizeMessage);
        }

        return new ParseOutcome<IFormFileCollection>(context.ErrorCount == errorsBefore, input);
    }

    /// <inheritdoc />
    /// <remarks>
    /// An array of whatever <see cref="Each"/> describes. The total size has no keyword — the dialect
    /// has nothing that adds up a property of the elements — so it is recorded as unrepresentable.
    /// </remarks>
    public override SchemaDescription Describe(DescriptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new SchemaDescription
        {
            Kind = SchemaKind.Array,
            MinItems = _minCount,
            MaxItems = _maxCount,
            Items = _each is null ? null : context.Describe(_each),
            Unrepresentable = _maxTotalSize is null
                ? null
                : ["a total size bound, which has no keyword that adds up the elements"],
        };
    }

    // The same two bounds on the asynchronous context. The synchronous one takes a ref struct, which
    // cannot cross an await, so the two cannot share a body -- which is the trade decision 8 made.
    private void CheckCountAsync(AsyncParseContext context, int count)
    {
        if (_minCount is { } minimum && count < minimum)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Collection,
                    Minimum = minimum,
                    Inclusive = true,
                },
                _minCountMessage);
        }

        if (_maxCount is { } maximum && count > maximum)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Collection,
                    Maximum = maximum,
                    Inclusive = true,
                },
                _maxCountMessage);
        }
    }

    private void CheckCount(ref ParseContext context, int count)
    {
        if (_minCount is { } minimum && count < minimum)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Collection,
                    Minimum = minimum,
                    Inclusive = true,
                },
                _minCountMessage);
        }

        if (_maxCount is { } maximum && count > maximum)
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Collection,
                    Maximum = maximum,
                    Inclusive = true,
                },
                _maxCountMessage);
        }
    }

    private FormFileCollectionSchema With(
        int? minCount = null,
        int? maxCount = null,
        long? maxTotalSize = null,
        Schema<IFormFile>? each = null,
        string? minCountMessage = null,
        string? maxCountMessage = null,
        string? maxTotalSizeMessage = null) =>
        new(
            minCount ?? _minCount,
            maxCount ?? _maxCount,
            maxTotalSize ?? _maxTotalSize,
            each ?? _each,
            minCountMessage ?? _minCountMessage,
            maxCountMessage ?? _maxCountMessage,
            maxTotalSizeMessage ?? _maxTotalSizeMessage);
}
