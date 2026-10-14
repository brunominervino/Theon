using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Theon.Tests;

/// <summary>
/// A format name the caller chose, in the document.
/// </summary>
/// <remarks>
/// <c>Matches(pattern, "cnpj")</c> carried the name as far as the error, which reads
/// <c>Invalid cnpj.</c>, and the document said only the pattern — so nothing generated from it knew
/// the rule had a name.
/// </remarks>
public class CallerNamedFormatTests
{
    private static readonly Regex Digits = new(@"\A\d{14}\z", RegexOptions.NonBacktracking);

    // The name goes to format, not to an x-format of our own. 2020-12 puts format in an annotation
    // vocabulary and requires an unrecognised value to be ignored rather than to fail, so a name of
    // the caller's own is safe there by specification; x-format would be safe too and nothing reads it.
    [Fact]
    public void A_Name_The_Caller_Chose_Goes_To_Format()
    {
        var document = Theo.String().Matches(Digits, "cnpj").ToJsonSchema();

        Assert.Equal("cnpj", document["format"]!.GetValue<string>());
    }

    // Both, because the pattern is the rule and the name is only its name. A validator enforces the
    // pattern and ignores the format; a reader and a code generator get the name.
    [Fact]
    public void The_Pattern_Goes_Out_Beside_It()
    {
        var document = Theo.String().Matches(Digits, "cnpj").ToJsonSchema();

        Assert.Equal(@"\A\d{14}\z", document["pattern"]!.GetValue<string>());
    }

    // No name given is no name to write. "regex" is what the parameter defaults to, not something
    // anybody chose.
    [Fact]
    public void An_Unnamed_Pattern_Writes_No_Format()
    {
        var document = Theo.String().Matches(Digits).ToJsonSchema();

        Assert.Null(document["format"]);
        Assert.Equal(@"\A\d{14}\z", document["pattern"]!.GetValue<string>());
    }

    // A caller naming a standard format gets the standard spelling, and the pattern is then the
    // redundant half.
    [Fact]
    public void A_Standard_Name_Still_Becomes_The_Standard_Format()
    {
        var document = Theo.String().Matches(Digits, "email").ToJsonSchema();

        Assert.Equal("email", document["format"]!.GetValue<string>());
        Assert.Null(document["pattern"]);
    }

    // The names this library chose for its own rules are unchanged. "format": "lowercase" says
    // nothing to any tool, and base64 has a spelling of its own in this dialect that is not format.
    [Theory]
    [InlineData("base64")]
    [InlineData("hex")]
    [InlineData("lowercase")]
    public void A_Name_This_Library_Chose_Is_Still_Written_As_The_Pattern_Alone(string which)
    {
        var schema = which switch
        {
            "base64" => Theo.String().Base64(),
            "hex" => Theo.String().Hex(),
            _ => Theo.String().Lowercase(),
        };

        var document = schema.ToJsonSchema();

        Assert.Null(document["format"]);
        Assert.NotNull(document["pattern"]);
    }

    [Fact]
    public void A_Named_Pattern_Survives_The_Round_Trip()
    {
        var document = Theo.String().Matches(Digits, "cnpj").ToJsonSchema();

        var again = Theo.JsonSchema(document).ToJsonSchema();

        Assert.True(
            JsonNode.DeepEquals(document, again),
            "out:  " + document.ToJsonString() + " back: " + again.ToJsonString());
    }
}
