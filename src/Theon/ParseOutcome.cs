namespace Theon;

/// <summary>
/// What an asynchronous schema produced: whether it succeeded, and the value if it did.
/// </summary>
/// <typeparam name="T">The type produced by the schema.</typeparam>
/// <remarks>
/// The synchronous path returns a <see langword="bool"/> and an <see langword="out"/> parameter,
/// which a <see cref="ValueTask{TResult}"/> cannot carry. This pairs them.
/// </remarks>
public readonly struct ParseOutcome<T>
{
    /// <summary>Creates an outcome.</summary>
    /// <param name="succeeded">Whether the schema produced a value.</param>
    /// <param name="value">The parsed value, when it did.</param>
    public ParseOutcome(bool succeeded, T value)
    {
        Succeeded = succeeded;
        Value = value;
    }

    /// <summary>Gets a value indicating whether the schema produced a value.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the parsed value. Meaningful only when <see cref="Succeeded"/> is true.</summary>
    public T Value { get; }
}
