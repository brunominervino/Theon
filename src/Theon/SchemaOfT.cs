namespace Theon;

/// <summary>
/// A schema that validates a value of type <typeparamref name="T"/> without changing its type.
/// </summary>
/// <typeparam name="T">The type validated by this schema.</typeparam>
/// <remarks>
/// This is the shape almost every schema has. It exists so that the common case reads as
/// <c>Schema&lt;string&gt;</c> rather than <c>Schema&lt;string, string&gt;</c>, while still being
/// usable anywhere a <see cref="Schema{TInput, TOutput}"/> is expected.
/// </remarks>
public abstract class Schema<T> : Schema<T, T>;
