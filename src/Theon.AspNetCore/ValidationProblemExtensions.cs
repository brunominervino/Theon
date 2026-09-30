using Theon.Errors;

namespace Theon.AspNetCore;

/// <summary>
/// Converts validation errors into the shape ASP.NET Core's problem responses expect.
/// </summary>
public static class ValidationProblemExtensions
{
    /// <summary>
    /// Renders errors as the dictionary carried by a <c>ValidationProblemDetails</c> response.
    /// </summary>
    /// <param name="errors">The errors to render.</param>
    /// <remarks>
    /// Keys are rendered paths, so a nested failure arrives as <c>Address.ZipCode</c> and an
    /// element failure as <c>Recipients[1]</c> — the same strings a client would use to address
    /// those values. Errors belonging to the object as a whole are filed under the empty key,
    /// which is the convention ASP.NET Core uses for model-level errors.
    /// </remarks>
    public static IDictionary<string, string[]> ToProblemDetailsErrors(
        this IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var flattened = errors.Flatten();

        return flattened
            .ToDictionary()
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
