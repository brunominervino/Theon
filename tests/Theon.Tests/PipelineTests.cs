using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// Text in, a typed value out, and rules on the value it became.
/// </summary>
public class PipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The shape the delegate exists for: a framework TryParse handed over as it is, with no lambda
    // wrapping it. If this stops compiling the delegate has the wrong signature.
    private static readonly Schema<string, int> PageSize =
        Theo.String().Trim().TryTransform<int>(
            int.TryParse,
            "Must be a whole number.",
            Theo.Int().Min(1).Max(100));

    [Fact]
    public void A_Transform_Can_Be_Followed_By_Rules_On_What_It_Produced()
    {
        var schema = Theo.String().Trim().Transform(int.Parse, Theo.Int().Min(1).Max(100));

        Assert.Equal(50, schema.Parse(" 50 "));
        Assert.False(schema.SafeParse("0").IsSuccess);
        Assert.False(schema.SafeParse("1000").IsSuccess);
    }

    [Fact]
    public void The_Follow_On_Rules_Report_Their_Own_Code()
    {
        var result = Theo.String().Transform(int.Parse, Theo.Int().Min(18)).SafeParse("5");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
        Assert.Equal("Must be greater than or equal to 18.", error.Message);
    }

    // The whole point of putting this at the field: the error has to name the property the caller
    // sent, not the number it became.
    [Fact]
    public void A_Follow_On_Failure_Reports_At_The_Path_The_Value_Came_From()
    {
        var schema = Theo.Object<Query>()
            .Field(x => x.PageSize, PageSize);

        var result = schema.SafeParse(new Query { PageSize = "1000" });

        Assert.Equal("PageSize", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void TryTransform_Reports_A_Failed_Conversion_Instead_Of_Throwing()
    {
        var result = PageSize.SafeParse("not a number");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("Must be a whole number.", error.Message);
    }

    // Before this existed the only way to parse text into a number was Transform with int.Parse,
    // which throws out of the middle of a parse and loses every other error in the same request.
    [Fact]
    public void TryTransform_Keeps_The_Other_Errors_In_The_Same_Parse()
    {
        var schema = Theo.Object<Query>()
            .Field(x => x.Sort, Theo.String().NotEmpty())
            .Field(x => x.PageSize, PageSize);

        var result = schema.SafeParse(new Query { Sort = "", PageSize = "nope" });

        Assert.Equal(["Sort", "PageSize"], result.Errors.Select(e => e.Path.ToString()));
    }

    [Fact]
    public void TryTransform_Runs_The_Follow_On_Rules_Only_After_Converting()
    {
        Assert.Equal(50, PageSize.Parse("50"));
        Assert.False(PageSize.SafeParse("0").IsSuccess);
        Assert.Equal(ValidationErrorCode.TooSmall, PageSize.SafeParse("0").Errors[0].Code);
    }

    [Fact]
    public void TryTransform_Needs_No_Follow_On_Schema()
    {
        var schema = Theo.String().TryTransform<Guid>(Guid.TryParse, "Not an identifier.");

        Assert.Equal(Guid.Empty, schema.Parse("00000000-0000-0000-0000-000000000000"));
        Assert.False(schema.SafeParse("nope").IsSuccess);
    }

    // The conversion never sees a value the schema before it already rejected, so it does not have to
    // defend against one.
    [Fact]
    public void The_Conversion_Does_Not_Run_When_The_Value_Was_Already_Rejected()
    {
        var attempts = 0;

        var schema = Theo.String().MinLength(3).TryTransform<int>(
            (string value, out int result) =>
            {
                attempts++;
                return int.TryParse(value, out result);
            },
            "Must be a whole number.");

        Assert.False(schema.SafeParse("ab").IsSuccess);
        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task The_Pipeline_Works_On_The_Asynchronous_Path()
    {
        Assert.Equal(50, await PageSize.ParseAsync("50", cancellationToken: Ct));
        Assert.False((await PageSize.SafeParseAsync("1000", cancellationToken: Ct)).IsSuccess);
        Assert.False((await PageSize.SafeParseAsync("nope", cancellationToken: Ct)).IsSuccess);
    }

    [Fact]
    public void Transform_And_TryTransform_Refuse_Their_Nulls()
    {
        Assert.Throws<ArgumentNullException>(() => Theo.String().Transform(int.Parse, null!));
        Assert.Throws<ArgumentNullException>(
            () => Theo.String().TryTransform<int>(null!, "unused"));
        Assert.Throws<ArgumentException>(
            () => Theo.String().TryTransform<int>(int.TryParse, string.Empty));
    }
}
