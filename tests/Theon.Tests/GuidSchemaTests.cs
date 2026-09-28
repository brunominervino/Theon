namespace Theon.Tests;

public class GuidSchemaTests
{
    [Fact]
    public void NotEmpty_Rejects_The_Empty_Guid()
    {
        var result = Theo.Guid().NotEmpty().SafeParse(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("Must not be the empty identifier.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void NotEmpty_Accepts_A_Real_Guid() =>
        Assert.True(Theo.Guid().NotEmpty().IsValid(Guid.NewGuid()));

    [Fact]
    public void A_Bare_Guid_Schema_Accepts_Everything_Including_Empty() =>
        Assert.True(Theo.Guid().IsValid(Guid.Empty));

    [Fact]
    public void AllowNull_Accepts_Null()
    {
        var schema = Theo.Guid().NotEmpty().AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.False(schema.SafeParse(Guid.Empty).IsSuccess);
    }
}
