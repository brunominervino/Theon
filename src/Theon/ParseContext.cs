using System.Runtime.CompilerServices;
using Theon.Errors;

namespace Theon;

[InlineArray(ParseContext.InlinePathCapacity)]
internal struct InlinePathBuffer
{
    private PathSegment _element0;
}

/// <summary>
/// Carries the state of one parse: where we are in the value, and what has gone wrong so far.
/// </summary>
/// <remarks>
/// <para>
/// This is a <see langword="ref struct"/> passed by reference through the whole parse. That is
/// deliberate: a valid value must not allocate. The path is held in an inline buffer covering the
/// first <see cref="InlinePathCapacity"/> levels of nesting, which covers essentially every real
/// object graph, and the error list is not created until the first error actually occurs.
/// </para>
/// <para>
/// Being a <see langword="ref struct"/> also makes the no-copy requirement a compiler error rather
/// than a silent bug: errors are recorded into this context, so a copy would quietly lose them.
/// </para>
/// </remarks>
public ref struct ParseContext
{
    internal const int InlinePathCapacity = 8;

    private InlinePathBuffer _inlinePath;
    private PathSegment[]? _overflowPath;
    private List<ValidationError>? _errors;
    private int _depth;

    internal ParseContext(ParseOptions options)
    {
        Options = options;
        _depth = 0;
    }

    internal ParseContext(ParseOptions options, ReadOnlySpan<PathSegment> basePath)
    {
        Options = options;
        _depth = 0;

        foreach (var segment in basePath)
        {
            Push(segment);
        }
    }

    /// <summary>Gets the options this parse was started with.</summary>
    public ParseOptions Options { get; }

    /// <summary>Gets the number of errors recorded so far.</summary>
    public readonly int ErrorCount => _errors?.Count ?? 0;

    /// <summary>Gets a value indicating whether any error has been recorded.</summary>
    public readonly bool HasErrors => _errors is { Count: > 0 };

    /// <summary>
    /// Gets a value indicating whether the parse should stop doing further work.
    /// </summary>
    /// <remarks>
    /// True only when the caller asked for <see cref="ParseOptions.StopOnFirstError"/> and an
    /// error has already been found. Composite schemas should check this between children.
    /// </remarks>
    public readonly bool ShouldStop => Options.StopOnFirstError && HasErrors;

    /// <summary>Enters the property <paramref name="name"/> for the purposes of error paths.</summary>
    /// <param name="name">The property name.</param>
    public void PushProperty(string name) => Push(PathSegment.Property(name));

    /// <summary>Enters the element at <paramref name="index"/> for the purposes of error paths.</summary>
    /// <param name="index">The zero-based element index.</param>
    public void PushIndex(int index) => Push(PathSegment.Index(index));

    /// <summary>Leaves the most recently entered property or element.</summary>
    public void Pop()
    {
        if (_depth > 0)
        {
            _depth--;
        }
    }

    private void Push(PathSegment segment)
    {
        if (_depth < InlinePathCapacity)
        {
            _inlinePath[_depth] = segment;
        }
        else
        {
            var overflowIndex = _depth - InlinePathCapacity;
            if (_overflowPath is null)
            {
                _overflowPath = new PathSegment[4];
            }
            else if (overflowIndex >= _overflowPath.Length)
            {
                Array.Resize(ref _overflowPath, _overflowPath.Length * 2);
            }

            _overflowPath[overflowIndex] = segment;
        }

        _depth++;
    }

    /// <summary>
    /// Records a validation failure at the current path.
    /// </summary>
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

    private readonly ValidationPath CapturePath()
    {
        if (_depth == 0)
        {
            return ValidationPath.Root;
        }

        var segments = new PathSegment[_depth];
        var inlineCount = Math.Min(_depth, InlinePathCapacity);
        for (var i = 0; i < inlineCount; i++)
        {
            segments[i] = _inlinePath[i];
        }

        for (var i = InlinePathCapacity; i < _depth; i++)
        {
            segments[i] = _overflowPath![i - InlinePathCapacity];
        }

        return new ValidationPath(segments);
    }

    internal List<ValidationError>? DrainErrors()
    {
        var errors = _errors;
        _errors = null;
        return errors;
    }

    internal IReadOnlyList<ValidationError> TakeErrors()
    {
        var errors = _errors;
        _errors = null;
        return errors is null ? Array.Empty<ValidationError>() : errors;
    }
}
