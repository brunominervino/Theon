using System.Runtime.InteropServices;
using Theon.Errors;

namespace Theon;

/// <summary>
/// Carries the state of one asynchronous parse.
/// </summary>
/// <remarks>
/// <para>
/// The synchronous <see cref="ParseContext"/> is a <see langword="ref struct"/>, which is what lets
/// a valid value parse without allocating — and which also means it cannot cross an
/// <see langword="await"/>. That is a language rule, not an oversight to work around, so the
/// asynchronous path gets its own context, and it is a class.
/// </para>
/// <para>
/// The cost is one allocation per asynchronous parse, which is noise beside the I/O that made the
/// parse asynchronous in the first place. The synchronous path is untouched and still allocates
/// nothing. See <c>docs/decisions/0008-asynchronous-validation.md</c>.
/// </para>
/// </remarks>
public sealed class AsyncParseContext
{
    private readonly List<PathSegment> _path = [];
    private List<ValidationError>? _errors;

    internal AsyncParseContext(ParseOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the options this parse was started with.</summary>
    public ParseOptions Options { get; }

    /// <summary>Gets the token that cancels this parse.</summary>
    /// <remarks>
    /// A rule that performs I/O should pass this on. A parse that reaches a database on behalf of a
    /// request that has already been abandoned is work nobody is waiting for.
    /// </remarks>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets the number of errors recorded so far.</summary>
    public int ErrorCount => _errors?.Count ?? 0;

    /// <summary>Gets a value indicating whether any error has been recorded.</summary>
    public bool HasErrors => _errors is { Count: > 0 };

    /// <summary>Gets a value indicating whether the parse should stop doing further work.</summary>
    public bool ShouldStop => Options.StopOnFirstError && HasErrors;

    /// <summary>Enters the property <paramref name="name"/> for the purposes of error paths.</summary>
    /// <param name="name">The property name.</param>
    public void PushProperty(string name) => _path.Add(PathSegment.Property(name));

    /// <summary>Enters the element at <paramref name="index"/> for the purposes of error paths.</summary>
    /// <param name="index">The zero-based element index.</param>
    public void PushIndex(int index) => _path.Add(PathSegment.Index(index));

    /// <summary>Leaves the most recently entered property or element.</summary>
    public void Pop()
    {
        if (_path.Count > 0)
        {
            _path.RemoveAt(_path.Count - 1);
        }
    }

    /// <summary>Records a validation failure at the current path.</summary>
    /// <param name="info">The structured facts about the failure.</param>
    /// <param name="message">
    /// An explicit message that overrides every provider, or <see langword="null"/> to resolve one.
    /// </param>
    public void AddError(in ValidationErrorInfo info, string? message = null)
    {
        var resolved = message
            ?? Options.MessageProvider?.Invoke(in info)
            ?? SchemaGlobalOptions.MessageProvider?.Invoke(in info)
            ?? DefaultErrorMessages.For(in info);

        (_errors ??= []).Add(new ValidationError(CapturePath(), resolved, in info));
    }

    private ValidationPath CapturePath() =>
        _path.Count == 0 ? ValidationPath.Root : new ValidationPath([.. _path]);

    // Runs a synchronous schema from within an asynchronous parse. The temporary context starts at
    // the path walked so far, so an error it raises still knows where it is, and its errors are
    // moved here rather than copied so nothing is reported twice.
    internal bool RunSynchronously<TInput, TOutput>(
        Schema<TInput, TOutput> schema,
        TInput input,
        out TOutput output)
    {
        var sync = new ParseContext(Options, CollectionsMarshal.AsSpan(_path));
        var succeeded = schema.TryParse(ref sync, input, out output);

        var raised = sync.DrainErrors();
        if (raised is { Count: > 0 })
        {
            (_errors ??= []).AddRange(raised);
        }

        return succeeded;
    }

    // Lends a synchronous context positioned at the current path, for running rules that are not
    // asynchronous from inside an asynchronous parse. Pair every call with EndSync.
    internal ParseContext BeginSync() => new(Options, CollectionsMarshal.AsSpan(_path));

    internal void EndSync(ref ParseContext sync)
    {
        var raised = sync.DrainErrors();
        if (raised is { Count: > 0 })
        {
            (_errors ??= []).AddRange(raised);
        }
    }

    internal IReadOnlyList<ValidationError> TakeErrors()
    {
        var errors = _errors;
        _errors = null;
        return errors is null ? Array.Empty<ValidationError>() : errors;
    }
}
