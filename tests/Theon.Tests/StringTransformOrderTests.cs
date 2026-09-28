namespace Theon.Tests;

/// <summary>
/// Transformations are rules in the chain, not a separate stage, so their position is meaningful.
/// </summary>
public class StringTransformOrderTests
{
    [Fact]
    public void Trim_Before_MinLength_Measures_The_Trimmed_Value()
    {
        var schema = Theo.String().Trim().MinLength(2);

        Assert.Equal("12", schema.Parse(" 12 "));
        Assert.False(schema.IsValid(" 1 "));
    }

    [Fact]
    public void Trim_After_MinLength_Measures_The_Original_Value()
    {
        var schema = Theo.String().MinLength(2).Trim();

        // " 1 " is three characters, so it satisfies MinLength(2); only then is it trimmed.
        Assert.Equal("1", schema.Parse(" 1 "));
    }

    [Fact]
    public void The_Transformed_Value_Is_What_Parse_Returns()
    {
        var schema = Theo.String().Trim().ToLowerInvariant();

        Assert.Equal("user@example.com", schema.Parse("  USER@Example.COM  "));
    }

    [Fact]
    public void ToLowerInvariant_Does_Not_Follow_The_Ambient_Culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

            // Turkish casing maps 'I' to a dotless 'i'; invariant casing must not.
            Assert.Equal("id", Theo.String().ToLowerInvariant().Parse("ID"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Normalize_Runs_In_Position()
    {
        var schema = Theo.String().Normalize(static v => v.Replace("-", string.Empty, StringComparison.Ordinal)).Length(10);

        Assert.True(schema.IsValid("123-456-7890"));
    }
}
