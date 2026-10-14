#if !NET9_0_OR_GREATER

namespace System.Text.Json.Serialization;

// The real attribute arrived in .NET 9 and this project also targets net8.0. Declared here for that
// target, under the same full name, so the enum in EnumMemberNameTests compiles on both.
//
// That makes the test better rather than worse: on net8.0 the library is matching an attribute from a
// different assembly that merely has the same name, which is precisely the mechanism it uses -- there
// is not one #if in src, and the attribute is found by its type name rather than by a reference to it.
[AttributeUsage(AttributeTargets.Field)]
internal sealed class JsonStringEnumMemberNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

#endif
