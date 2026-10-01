using Theon.Metadata;

namespace Theon.Checks;

// Builds the description of a schema whose shape is a kind plus whatever its rules contribute.
// Nearly every leaf schema is that, so this saves each of them writing the same loop.
internal static class CheckDescription
{
    // Builds the mutable half and hands back the immutable one, which is the only shape the rest of the
    // library and anything outside it ever sees.
    internal static SchemaDescription Of<T>(SchemaKind kind, Check<T>[] checks, string? format = null) =>
        Build(kind, checks, format).ToDescription();

    // For the schemas that have something of their own to add -- children, properties, a reference --
    // after their rules have had their say.
    internal static SchemaDescriptionBuilder Build<T>(
        SchemaKind kind,
        Check<T>[] checks,
        string? format = null)
    {
        var description = new SchemaDescriptionBuilder { Kind = kind, Format = format };

        foreach (var check in checks)
        {
            check.Describe(description);
        }

        return description;
    }
}
