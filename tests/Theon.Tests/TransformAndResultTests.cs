namespace Theon.Tests;

public class TransformAndResultTests
{
    [Fact]
    public void Transform_Changes_The_Output_Type()
    {
        Schema<string, int> schema = Theo.String().Trim().Transform(int.Parse);

        Assert.Equal(42, schema.Parse("  42  "));
    }

    [Fact]
    public void Transform_Does_Not_Run_When_Validation_Failed()
    {
        var ran = false;
        var schema = Theo.String().MinLength(5).Transform(value =>
        {
            ran = true;
            return value.Length;
        });

        var result = schema.SafeParse("ab");

        Assert.False(result.IsSuccess);
        Assert.False(ran);
    }

    [Fact]
    public void Transform_Sees_The_Normalized_Value()
    {
        var schema = Theo.String().Trim().Transform(static v => v.Length);

        Assert.Equal(2, schema.Parse("  ab  "));
    }

    [Fact]
    public void TryGetValue_Reports_Success_And_The_Value()
    {
        Assert.True(Theo.String().SafeParse("ok").TryGetValue(out var value));
        Assert.Equal("ok", value);

        Assert.False(Theo.String().MinLength(5).SafeParse("ab").TryGetValue(out _));
    }

    [Fact]
    public void ValueOrThrow_Throws_On_Failure()
    {
        var result = Theo.String().MinLength(5).SafeParse("ab");

        Assert.Throws<SchemaValidationException>(() => result.ValueOrThrow());
    }

    [Fact]
    public void A_Successful_Result_Has_No_Errors()
    {
        var result = Theo.String().SafeParse("ok");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal("ok", result.ValueOrThrow());
    }

    [Fact]
    public void IsValid_Agrees_With_SafeParse()
    {
        var schema = Theo.String().MinLength(3).Email();

        foreach (var value in new[] { "a@b.co", "ab", string.Empty, "user@example.com" })
        {
            Assert.Equal(schema.SafeParse(value).IsSuccess, schema.IsValid(value));
        }
    }
}
