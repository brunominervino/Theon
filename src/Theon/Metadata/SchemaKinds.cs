namespace Theon.Metadata;

// Maps a CLR type to the kind of JSON value it travels as.
//
// Written out rather than discovered, because discovering it would mean asking the type system
// questions at run time that trimming is entitled to have removed the answers to.
internal static class SchemaKinds
{
    internal static SchemaKind For(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;

        if (actual.IsEnum)
        {
            // An enum travels as its member name once a string converter is in play, which is what a
            // documented API uses. See EnumSchema, which says so where a caller will read it.
            return SchemaKind.String;
        }

        if (actual == typeof(string) || actual == typeof(char) || actual == typeof(Guid) ||
            actual == typeof(Uri) || actual == typeof(DateTime) || actual == typeof(DateTimeOffset) ||
            actual == typeof(DateOnly) || actual == typeof(TimeOnly) || actual == typeof(TimeSpan))
        {
            return SchemaKind.String;
        }

        if (actual == typeof(bool))
        {
            return SchemaKind.Boolean;
        }

        if (actual == typeof(byte) || actual == typeof(sbyte) || actual == typeof(short) ||
            actual == typeof(ushort) || actual == typeof(int) || actual == typeof(uint) ||
            actual == typeof(long) || actual == typeof(ulong) || actual == typeof(nint) ||
            actual == typeof(nuint) || actual == typeof(Int128) || actual == typeof(UInt128) ||
            actual == typeof(System.Numerics.BigInteger))
        {
            return SchemaKind.Integer;
        }

        if (actual == typeof(decimal) || actual == typeof(double) || actual == typeof(float) ||
            actual == typeof(Half))
        {
            return SchemaKind.Number;
        }

        return SchemaKind.Unknown;
    }
}
