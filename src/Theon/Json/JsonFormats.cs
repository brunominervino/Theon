namespace Theon.Json;

// The format names a document may carry, and the rule this library already has for each.
//
// Single-sourced on purpose. A second e-mail rule written for the reading side would drift from the
// one on the writing side, and then a schema would stop accepting the documents it generates.
internal static class JsonFormats
{
    private static readonly Dictionary<string, Schema<string>> Known =
        new(StringComparer.Ordinal)
        {
            ["email"] = Theo.String().Email(),
            ["url"] = Theo.String().Url(),
            ["uuid"] = Theo.String().Uuid(),
            ["base64"] = Theo.String().Base64(),
            ["base64url"] = Theo.String().Base64Url(),
            ["hex"] = Theo.String().Hex(),
            ["e164"] = Theo.String().E164(),
            ["iso8601"] = Theo.String().Iso8601(),
            ["date-time"] = Theo.String().Iso8601(),
            ["iso8601_date"] = Theo.String().Iso8601Date(),
            ["date"] = Theo.String().Iso8601Date(),
            ["iso8601_time"] = Theo.String().Iso8601Time(),
            ["iso8601_duration"] = Theo.String().Iso8601Duration(),
            ["duration"] = Theo.String().Iso8601Duration(),
            ["ipv4"] = Theo.String().Ipv4(),
            ["ipv6"] = Theo.String().Ipv6(),
            ["cidr"] = Theo.String().Cidr(),
            ["hostname"] = Theo.String().Hostname(),
            ["jwt"] = Theo.String().Jwt(),
            ["credit_card"] = Theo.String().CreditCard(),
            ["iban"] = Theo.String().Iban(),
        };

    // An unknown format is ignored rather than refused, which is the one place this reader lets
    // something through. It is not a hole: 2020-12 makes "format" an annotation unless the
    // format-assertion vocabulary is in use, so a validator ignoring one it does not know is doing
    // what the dialect says. Refusing would also make this useless on real documents, which are full
    // of "int64", "password" and names nobody has written a rule for.
    internal static Schema<string>? For(string format) =>
        Known.TryGetValue(format, out var schema) ? schema : null;
}
