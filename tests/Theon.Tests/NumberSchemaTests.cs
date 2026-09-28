using Theon.Errors;

namespace Theon.Tests;

public class NumberSchemaTests
{
    [Fact]
    public void Min_And_Max_Are_Inclusive()
    {
        var schema = Theo.Int().Min(18).Max(120);

        Assert.True(schema.IsValid(18));
        Assert.True(schema.IsValid(120));
        Assert.False(schema.IsValid(17));
        Assert.False(schema.IsValid(121));
    }

    [Fact]
    public void GreaterThan_And_LessThan_Are_Exclusive()
    {
        Assert.False(Theo.Int().GreaterThan(0).IsValid(0));
        Assert.True(Theo.Int().GreaterThan(0).IsValid(1));
        Assert.False(Theo.Int().LessThan(10).IsValid(10));
        Assert.True(Theo.Int().LessThan(10).IsValid(9));
    }

    [Fact]
    public void Positive_Excludes_Zero_And_NonNegative_Includes_It()
    {
        Assert.False(Theo.Int().Positive().IsValid(0));
        Assert.True(Theo.Int().NonNegative().IsValid(0));
        Assert.False(Theo.Int().Negative().IsValid(0));
        Assert.True(Theo.Int().NonPositive().IsValid(0));
    }

    [Fact]
    public void The_Error_Carries_The_Bound_And_Its_Inclusivity()
    {
        var result = Theo.Int().Min(18).SafeParse(17);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
        Assert.Equal(ValidationOrigin.Number, error.Origin);
        Assert.Equal(18, error.Info.Minimum);
        Assert.True(error.Info.Inclusive);
        Assert.Equal("Must be greater than or equal to 18.", error.Message);
    }

    [Fact]
    public void An_Exclusive_Bound_Says_So_In_The_Message()
    {
        var result = Theo.Int().GreaterThan(0).SafeParse(0);

        Assert.Equal("Must be greater than 0.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void MultipleOf_Uses_An_Exact_Remainder()
    {
        Assert.True(Theo.Int().MultipleOf(5).IsValid(20));
        Assert.False(Theo.Int().MultipleOf(5).IsValid(21));
        Assert.True(Theo.Int().MultipleOf(5).IsValid(0));
        Assert.True(Theo.Int().MultipleOf(5).IsValid(-20));
    }

    [Fact]
    public void MultipleOf_Rejects_A_Zero_Divisor() =>
        Assert.Throws<ArgumentOutOfRangeException>(static () => Theo.Int().MultipleOf(0));

    [Fact]
    public void MultipleOf_On_Decimal_Handles_Decimal_Steps()
    {
        Assert.True(Theo.Decimal().MultipleOf(0.01m).IsValid(19.99m));
        Assert.False(Theo.Decimal().MultipleOf(0.10m).IsValid(19.99m));
    }

    [Fact]
    public void NaN_Is_Rejected_Before_Any_Bound_Is_Considered()
    {
        // A NaN compares false against every bound, so a range check alone would let it through.
        var result = Theo.Double().Min(0).Max(1).SafeParse(double.NaN);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("NaN", error.Info.Received);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void The_Infinities_Are_Rejected(double value) =>
        Assert.False(Theo.Double().IsValid(value));

    [Fact]
    public void An_Ordinary_Double_Is_Accepted() => Assert.True(Theo.Double().IsValid(1.5));

    [Fact]
    public void NegativeZero_Is_Finite_And_NonNegative()
    {
        Assert.True(Theo.Double().NonNegative().IsValid(-0.0));
        Assert.False(Theo.Double().Positive().IsValid(-0.0));
    }

    [Fact]
    public void Generic_Math_Covers_Other_Numeric_Types()
    {
        Assert.True(Theo.Long().Min(1L).IsValid(long.MaxValue));
        Assert.True(Theo.Number<short>().Max((short)10).IsValid((short)5));
        Assert.False(Theo.Number<byte>().Min((byte)10).IsValid((byte)9));
    }

    [Fact]
    public void AllowNull_Produces_A_Nullable_Schema()
    {
        var schema = Theo.Int().Min(18).AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.True(schema.SafeParse(20).IsSuccess);
        Assert.False(schema.SafeParse(17).IsSuccess);
    }
}
