using Theon.Metadata;

namespace Theon.Checks;

// Builds the description of a schema whose shape is a kind plus whatever its rules contribute.
// Nearly every leaf schema is that, so this saves each of them writing the same loop.
internal static class CheckDescription
{
    internal static SchemaDescription Of<T>(SchemaKind kind, Check<T>[] checks, string? format = null)
    {
        var description = new SchemaDescription { Kind = kind, Format = format };

        foreach (var check in checks)
        {
            check.Describe(description);
        }

        return description;
    }
}
