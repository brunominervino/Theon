using Theon.Checks;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates a <see cref="Uri"/>.
/// </summary>
/// <remarks>
/// <para>
/// For a value that is already a <see cref="Uri"/> — a configuration binding, a webhook target, a
/// redirect the caller supplied. The syntax was settled when the instance was constructed, so what
/// remains is whether it is the <em>kind</em> of address this field accepts.
/// </para>
/// <para>
/// Where the value is a <see cref="string"/> that ought to be an address, use
/// <c>Theo.String().Url()</c> instead. It answers a different question — is this text a web
/// address a person meant to type — and it answers it without constructing anything.
/// </para>
/// </remarks>
public sealed class UriSchema : Schema<Uri>
{
    // Uri has no Empty, and the out parameter has to hold something on the failure path. Allocated
    // once, never returned to a caller who did not already get false.
    private static readonly Uri RelativeSentinel = new(string.Empty, UriKind.Relative);

    private readonly Check<Uri>[] _checks;

    internal UriSchema()
        : this([])
    {
    }

    private UriSchema(Check<Uri>[] checks) => _checks = checks;

    private UriSchema With(Check<Uri> check) => new([.. _checks, check]);

    /// <summary>Requires an absolute address.</summary>
    /// <param name="message">A message that replaces the default for this rule.</param>
    /// <remarks>
    /// Worth asking explicitly: <c>new Uri("/api/things", UriKind.Relative)</c> is a perfectly good
    /// <see cref="Uri"/> and a useless webhook target. A failure here stops the rules that follow,
    /// because almost everything else about a <see cref="Uri"/> — scheme, host, port — throws on a
    /// relative one.
    /// </remarks>
    public UriSchema Absolute(string? message = null) =>
        With(new AbsoluteUriCheck { Message = message });

    /// <summary>Requires the address to use one of <paramref name="schemes"/>.</summary>
    /// <param name="schemes">
    /// The allowed schemes, lowercase and without a colon, as <see cref="Uri.Scheme"/> reports them:
    /// <c>"https"</c>, not <c>"HTTPS:"</c>.
    /// </param>
    /// <remarks>
    /// A relative address fails this rule rather than throwing, so it is safe to write on its own.
    /// </remarks>
    /// <example>
    /// <code>
    /// Theo.Uri().Absolute().Scheme("https");
    /// Theo.Uri().Scheme("http", "https");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException"><paramref name="schemes"/> was empty.</exception>
    public UriSchema Scheme(params string[] schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);

        if (schemes.Length == 0)
        {
            throw new ArgumentException(
                "Name at least one scheme; a rule that allows none can never pass.",
                nameof(schemes));
        }

        // Copied, so a caller who keeps and then mutates the array cannot change the rule after the
        // fact. Schemas are immutable, and that has to hold against the array as well as the schema.
        var allowed = schemes.ToArray();
        return With(new UriSchemeCheck(allowed, string.Join(", ", allowed)));
    }

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    public UriSchema Refine(Func<Uri, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return With(new RefineCheck<Uri>(predicate, message));
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<Uri?> AllowNull() => new NullableReferenceSchema<Uri>(this);

    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context) =>
        CheckDescription.Of(SchemaKind.String, _checks, "uri-reference");

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, Uri input, out Uri output)
    {
        var errorsBefore = context.ErrorCount;

        if (input is null)
        {
            context.AddError(new ValidationErrorInfo
            {
                Code = ValidationErrorCode.InvalidType,
                Expected = "an address",
                Received = "null",
            });

            output = RelativeSentinel;
            return false;
        }

        output = input;

        var aborted = false;
        foreach (var check in _checks)
        {
            if (aborted || context.ShouldStop)
            {
                break;
            }

            var before = context.ErrorCount;
            check.Run(ref context, ref output);

            if (context.ErrorCount > before && check.AbortsOnFailure)
            {
                aborted = true;
            }
        }

        return context.ErrorCount == errorsBefore;
    }
}
