using System.Text;
using Theon.Errors;

namespace Theon;

/// <summary>
/// Thrown by <see cref="Schema{TInput, TOutput}.Parse"/> when a value does not satisfy its schema.
/// </summary>
public sealed class SchemaValidationException : Exception
{
    /// <summary>Creates an exception carrying the given errors.</summary>
    /// <param name="errors">The errors that caused the failure. Must not be empty.</param>
    public SchemaValidationException(IReadOnlyList<ValidationError> errors)
        : base(BuildMessage(errors))
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    /// <summary>Gets the errors that caused the failure.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return "Validation failed.";
        }

        if (errors.Count == 1)
        {
            return errors[0].ToString();
        }

        var builder = new StringBuilder("Validation failed with ")
            .Append(errors.Count)
            .Append(" errors:");

        foreach (var error in errors)
        {
            builder.Append("\n  ").Append(error.ToString());
        }

        return builder.ToString();
    }
}
