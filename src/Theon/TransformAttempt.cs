namespace Theon;

/// <summary>
/// Attempts to turn a value of one type into another, reporting failure rather than throwing.
/// </summary>
/// <typeparam name="TFrom">The type to convert from.</typeparam>
/// <typeparam name="TTo">The type to convert to.</typeparam>
/// <param name="value">The value to convert.</param>
/// <param name="result">The converted value on success; otherwise <see langword="default"/>.</param>
/// <returns><see langword="true"/> if the value was converted.</returns>
/// <remarks>
/// Shaped like <c>TryParse</c> on purpose, so the framework methods that already have this shape can
/// be handed over as they are: <c>int.TryParse</c>, <c>Guid.TryParse</c>,
/// <c>DateTimeOffset.TryParse</c>. A conversion that throws instead would turn a value a person typed
/// wrongly into an exception, which is the one thing this library exists not to do.
/// </remarks>
public delegate bool TransformAttempt<in TFrom, TTo>(TFrom value, out TTo result);
