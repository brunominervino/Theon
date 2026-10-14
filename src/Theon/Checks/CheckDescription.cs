using Theon.Metadata;

namespace Theon.Checks;

// Builds the description of a schema whose shape is a kind plus whatever its rules contribute.
// Nearly every leaf schema is that, so this saves each of them writing the same loop.
internal static class CheckDescription
{
    // Builds the mutable half and hands back the immutable one, which is the only shape the rest of the
    // library and anything outside it ever sees.
    internal static SchemaDescription Of<T>(
        DescriptionContext context,
        SchemaKind kind,
        Check<T>[] checks,
        string? format = null) =>
        Build(context, kind, checks, format).ToDescription();

    // For the schemas that have something of their own to add -- children, properties, a reference --
    // after their rules have had their say.
    //
    // A rule is only claimed on the side of the value it was actually checked against. Rules run in
    // the order they were written, and a normalization rewrites the value for everything after it, so
    // on the input side the rules after the last normalization describe something that never arrived,
    // and on the output side the rules before it describe something that no longer exists.
    // Trim().MaxLength(14) accepts fifteen characters where one is a space; MinLength(3).Trim()
    // produces a single character out of "  a  ". Either claim makes the document stricter than the
    // schema, which is the one direction that turns a generated client into a second, wrong validator.
    //
    // What is dropped is recorded rather than discarded, so a caller whose document is the contract is
    // told the constraint is not in it.
    internal static SchemaDescriptionBuilder Build<T>(
        DescriptionContext context,
        SchemaKind kind,
        Check<T>[] checks,
        string? format = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var description = new SchemaDescriptionBuilder { Kind = kind, Format = format };
        var describing = Claimable(context.Direction, checks);

        for (var i = 0; i < checks.Length; i++)
        {
            if (describing[i])
            {
                checks[i].Describe(description);
            }
            else
            {
                description.CannotRepresent(checks[i].RuleName);
            }
        }

        return description;
    }

    // Which rules describe the side being asked about. A normalization itself is claimable: it has
    // nothing to claim, and its own Describe already records that it could not be expressed.
    private static bool[] Claimable<T>(DescriptionDirection direction, Check<T>[] checks)
    {
        var claimable = new bool[checks.Length];
        Array.Fill(claimable, true);

        if (direction == DescriptionDirection.Input)
        {
            var rewritten = false;

            for (var i = 0; i < checks.Length; i++)
            {
                claimable[i] = !rewritten || checks[i].Rewrites;
                rewritten |= checks[i].Rewrites;
            }

            return claimable;
        }

        var rewrittenLater = false;

        for (var i = checks.Length - 1; i >= 0; i--)
        {
            claimable[i] = !rewrittenLater || checks[i].Rewrites;
            rewrittenLater |= checks[i].Rewrites;
        }

        return claimable;
    }
}
