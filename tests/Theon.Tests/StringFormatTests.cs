using Theon.Errors;

namespace Theon.Tests;

public class StringFormatTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@example.co.uk")]
    [InlineData("user+tag@example.com")]
    [InlineData("u@e.io")]
    [InlineData("a_b-c@sub.domain.example.com")]
    public void Email_Accepts_Ordinary_Addresses(string value) =>
        Assert.True(Theo.String().Email().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("plainstring")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@example")]
    [InlineData("user@@example.com")]
    [InlineData("user name@example.com")]
    [InlineData(".user@example.com")]
    [InlineData("user.@example.com")]
    [InlineData("user@.example.com")]
    [InlineData("user@example..com")]
    public void Email_Rejects_Malformed_Addresses(string value) =>
        Assert.False(Theo.String().Email().IsValid(value));

    [Fact]
    public void Email_Reports_The_Format_Name()
    {
        var result = Theo.String().Email().SafeParse("nope");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidFormat, error.Code);
        Assert.Equal("email", error.Format);
        Assert.Equal("Invalid e-mail address.", error.Message);
    }

    [Fact]
    public void Email_Is_Not_Fooled_By_A_Newline()
    {
        // An unanchored pattern would match the first line and let the rest through.
        Assert.False(Theo.String().Email().IsValid("user@example.com\nnot-an-email"));
    }

    [Fact]
    public void StartsWith_And_EndsWith_And_Contains()
    {
        Assert.True(Theo.String().StartsWith("ab").IsValid("abc"));
        Assert.False(Theo.String().StartsWith("ab").IsValid("xabc"));
        Assert.True(Theo.String().EndsWith("bc").IsValid("abc"));
        Assert.False(Theo.String().EndsWith("bc").IsValid("abcx"));
        Assert.True(Theo.String().Contains("b").IsValid("abc"));
        Assert.False(Theo.String().Contains("z").IsValid("abc"));
    }

    [Fact]
    public void Substring_Rules_Are_Ordinal_By_Default()
    {
        Assert.False(Theo.String().StartsWith("AB").IsValid("abc"));
        Assert.True(Theo.String().StartsWith("AB", StringComparison.OrdinalIgnoreCase).IsValid("abc"));
    }

    [Fact]
    public void Lowercase_And_Uppercase()
    {
        Assert.True(Theo.String().Lowercase().IsValid("abc123"));
        Assert.False(Theo.String().Lowercase().IsValid("abC"));
        Assert.True(Theo.String().Uppercase().IsValid("ABC123"));
        Assert.False(Theo.String().Uppercase().IsValid("ABc"));
    }

    [Fact]
    public void Refine_Reports_Its_Own_Message()
    {
        var schema = Theo.String().Refine(static v => v.Length % 2 == 0, "Must have an even length.");

        var result = schema.SafeParse("abc");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.Custom, error.Code);
        Assert.Equal("Must have an even length.", error.Message);
    }

    [Fact]
    public void A_Rule_Message_Overrides_The_Default()
    {
        var result = Theo.String().MinLength(5, "Too short, sorry.").SafeParse("ab");

        Assert.Equal("Too short, sorry.", Assert.Single(result.Errors).Message);
    }
}
