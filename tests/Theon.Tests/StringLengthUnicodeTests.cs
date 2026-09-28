namespace Theon.Tests;

/// <summary>
/// Length is measured in Unicode code points, not UTF-16 code units. A user who typed one emoji
/// typed one character, and must not be told they typed two.
/// </summary>
public class StringLengthUnicodeTests
{
    private const string Grin = "😀";           // U+1F600, one code point, two UTF-16 units
    private const string FiveGrins = Grin + Grin + Grin + Grin + Grin;

    [Fact]
    public void A_Surrogate_Pair_Counts_As_One_Character()
    {
        Assert.Equal(2, Grin.Length);
        Assert.True(Theo.String().Length(1).IsValid(Grin));
    }

    [Fact]
    public void MaxLength_Counts_Code_Points()
    {
        Assert.Equal(10, FiveGrins.Length);
        Assert.True(Theo.String().MaxLength(5).IsValid(FiveGrins));
        Assert.False(Theo.String().MaxLength(5).IsValid(FiveGrins + Grin));
    }

    [Fact]
    public void MinLength_Counts_Code_Points()
    {
        Assert.False(Theo.String().MinLength(5).IsValid(Grin + Grin + Grin + Grin));
        Assert.True(Theo.String().MinLength(5).IsValid(FiveGrins));
    }

    [Fact]
    public void A_Combining_Mark_Is_Its_Own_Code_Point()
    {
        // "e" followed by a combining acute accent renders as one glyph but is two code points.
        // Grapheme clustering is culture- and Unicode-version-dependent; code points are stable.
        Assert.True(Theo.String().Length(2).IsValid("é"));
    }

    [Fact]
    public void An_Unpaired_Surrogate_Counts_As_One()
    {
        Assert.True(Theo.String().Length(1).IsValid("\uD83D"));
        Assert.True(Theo.String().Length(2).IsValid("\uDE00\uD83D"));
    }

    [Fact]
    public void Ascii_Takes_The_Fast_Path_And_Still_Agrees()
    {
        Assert.True(Theo.String().Length(5).IsValid("hello"));
        Assert.False(Theo.String().Length(5).IsValid("hell"));
        Assert.False(Theo.String().Length(5).IsValid("hello!"));
    }

    [Fact]
    public void The_Reported_Bound_Is_The_Declared_One()
    {
        var result = Theo.String().MaxLength(5).SafeParse(FiveGrins + Grin);

        var error = Assert.Single(result.Errors);
        Assert.Equal(5, error.Info.Maximum);
    }
}
