using System.Text.RegularExpressions;

namespace Theon.Tests;

/// <summary>
/// What a document may claim about a value that is normalized on the way in.
/// </summary>
/// <remarks>
/// <para>
/// A rule written after a normalization is checked against the normalized value, not against what
/// arrived. <c>Trim().MaxLength(14)</c> accepts fifteen characters where one of them is a space, and
/// <c>Trim().ToUpperInvariant().Matches(upperCaseOnly)</c> accepts lower case. A document claiming
/// those constraints of the input describes a server stricter than the real one, and a client
/// generated from it refuses values the server would have taken.
/// </para>
/// <para>
/// Decision 12 said a normalization is not modelled and described both sides identically. That was
/// the gap: being silent about the normalization is fine, and carrying the post-normalization
/// constraints onto the input side is not.
/// </para>
/// </remarks>
public class NormalizedInputDescriptionTests
{
    private static readonly Regex UpperCaseOnly = new(@"\A[A-Z]+\z", RegexOptions.NonBacktracking);

    private static readonly JsonSchemaOptions Output =
        new() { Direction = DescriptionDirection.Output };

    private static readonly JsonSchemaOptions Strict =
        new() { OnUnrepresentable = UnrepresentablePolicy.Throw };

    // The case from the field. A CNPJ arrives with spaces at the ends and in lower case, both of which
    // the server fixes before checking the pattern; the document used to claim the pattern of the
    // input, so a generated client refused exactly those values.
    [Fact]
    public void A_Pattern_After_A_Normalization_Is_Not_Claimed_Of_The_Input()
    {
        var schema = Theo.String().Trim().ToUpperInvariant().Matches(UpperCaseOnly, "codigo");

        // The server really does accept this.
        Assert.True(schema.SafeParse("  abc  ").IsSuccess);

        var document = schema.ToJsonSchema();

        Assert.Null(document["pattern"]);
        Assert.Null(document["format"]);
    }

    [Fact]
    public void A_Length_Bound_After_A_Normalization_Is_Not_Claimed_Of_The_Input()
    {
        var schema = Theo.String().Trim().MaxLength(3);

        Assert.True(schema.SafeParse("  abc  ").IsSuccess);

        Assert.Null(schema.ToJsonSchema()["maxLength"]);
    }

    // Omitting a rule is incomplete, and this library says so rather than going quiet. A caller whose
    // document is the contract finds out that the constraint is not in it.
    [Fact]
    public void What_Was_Dropped_Is_Reported()
    {
        var schema = Theo.String().Trim().MaxLength(3);

        var exception = Assert.Throws<InvalidOperationException>(() => schema.ToJsonSchema(Strict));

        Assert.Contains("MaxLengthCheck", exception.Message, StringComparison.Ordinal);
    }

    // A rule written before the normalization is checked against what arrived, so it is exactly an
    // input constraint and stays.
    [Fact]
    public void A_Rule_Before_The_Normalization_Is_Still_Claimed()
    {
        var document = Theo.String().MaxLength(10).Trim().ToJsonSchema();

        Assert.Equal(10, document["maxLength"]!.GetValue<int>());
    }

    [Fact]
    public void Only_The_Rules_After_It_Are_Dropped()
    {
        var document = Theo.String().MinLength(2).Trim().MaxLength(3).ToJsonSchema();

        Assert.Equal(2, document["minLength"]!.GetValue<int>());
        Assert.Null(document["maxLength"]);
    }

    // The mirror image, and the reason this is not just an input fix. On the way out the value has
    // been normalized, so a rule written before the normalization may no longer hold of it:
    // MinLength(3) passes "  a  " and the trimmed output is one character long.
    [Fact]
    public void On_The_Output_Side_It_Is_The_Rules_Before_It_That_Go()
    {
        var schema = Theo.String().MinLength(3).Trim().MaxLength(3);

        Assert.True(schema.SafeParse("  a  ").IsSuccess);
        Assert.Equal("a", schema.Parse("  a  "));

        var document = schema.ToJsonSchema(Output);

        Assert.Null(document["minLength"]);
        Assert.Equal(3, document["maxLength"]!.GetValue<int>());
    }

    [Fact]
    public void A_Rule_After_The_Normalization_Is_Exact_On_The_Output_Side()
    {
        var document = Theo.String().Trim().Matches(UpperCaseOnly, "codigo").ToJsonSchema(Output);

        Assert.Equal("codigo", document["format"]!.GetValue<string>());
        Assert.NotNull(document["pattern"]);
    }

    // Nothing changes for a schema that does not normalize, which is almost all of them.
    [Fact]
    public void A_Schema_With_No_Normalization_Is_Described_As_It_Always_Was()
    {
        var schema = Theo.String().MinLength(2).MaxLength(10).Matches(UpperCaseOnly, "codigo");

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(2, input["minLength"]!.GetValue<int>());
        Assert.Equal(10, input["maxLength"]!.GetValue<int>());
        Assert.Equal("codigo", input["format"]!.GetValue<string>());
        Assert.Equal(input.ToJsonString(), output.ToJsonString());
    }

    // The type survives either way. It is the one thing a normalization cannot change.
    [Fact]
    public void The_Type_Is_Always_Said()
    {
        var schema = Theo.String().Trim().MaxLength(3);

        Assert.Equal("string", schema.ToJsonSchema()["type"]!.GetValue<string>());
        Assert.Equal("string", schema.ToJsonSchema(Output)["type"]!.GetValue<string>());
    }

    [Fact]
    public void Normalizing_By_Hand_Counts_Too()
    {
        var schema = Theo.String().Normalize(static value => value.Replace(".", string.Empty, StringComparison.Ordinal)).MaxLength(3);

        Assert.True(schema.SafeParse("1.2.3").IsSuccess);
        Assert.Null(schema.ToJsonSchema()["maxLength"]);
    }
}
