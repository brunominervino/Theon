using Theon.Errors;

namespace Theon.Tests;

public class StringSchemaTests
{
    [Fact]
    public void Parse_Returns_The_Value_When_Every_Rule_Passes()
    {
        var schema = Theo.String().MinLength(3).MaxLength(10);

        Assert.Equal("hello", schema.Parse("hello"));
    }

    [Fact]
    public void Null_Fails_As_An_Invalid_Type()
    {
        var result = Theo.String().SafeParse(null!);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("A value is required.", error.Message);
    }

    [Fact]
    public void AllowNull_Accepts_Null()
    {
        var schema = Theo.String().MinLength(3).AllowNull();

        var result = schema.SafeParse(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AllowNull_Still_Applies_The_Rules_To_A_Value()
    {
        var schema = Theo.String().MinLength(3).AllowNull();

        Assert.False(schema.SafeParse("ab").IsSuccess);
        Assert.True(schema.SafeParse("abc").IsSuccess);
    }

    [Fact]
    public void MinLength_Reports_The_Declared_Bound_Not_The_Measurement()
    {
        var result = Theo.String().MinLength(5).SafeParse("ab");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
        Assert.Equal(ValidationOrigin.Text, error.Origin);
        Assert.Equal(5, error.Info.Minimum);
        Assert.True(error.Info.Inclusive);
    }

    [Fact]
    public void MaxLength_Reports_TooBig()
    {
        var result = Theo.String().MaxLength(2).SafeParse("abc");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooBig, error.Code);
        Assert.Equal(2, error.Info.Maximum);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("ab", 2)]
    public void Length_Accepts_Only_The_Exact_Length(string value, int length) =>
        Assert.True(Theo.String().Length(length).IsValid(value));

    [Fact]
    public void NotEmpty_Rejects_The_Empty_String() =>
        Assert.False(Theo.String().NotEmpty().IsValid(string.Empty));

    [Fact]
    public void NotEmpty_Accepts_Whitespace_Because_It_Is_Not_Empty() =>
        Assert.True(Theo.String().NotEmpty().IsValid(" "));
}
