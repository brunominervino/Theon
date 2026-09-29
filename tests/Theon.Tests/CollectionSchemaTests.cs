using Theon.Errors;

namespace Theon.Tests;

public class CollectionSchemaTests
{
    [Fact]
    public void A_Valid_List_Passes()
    {
        var schema = Theo.Collection(Theo.String().Email());

        Assert.True(schema.IsValid(new[] { "a@b.co", "c@d.io" }));
    }

    [Fact]
    public void The_Failing_Element_Is_Identified_By_Index()
    {
        var schema = Theo.Collection(Theo.String().Email());

        var result = schema.SafeParse(new[] { "a@b.co", "nope", "c@d.io" });

        var error = Assert.Single(result.Errors);
        Assert.Equal("[1]", error.Path.ToString());
    }

    [Fact]
    public void Every_Failing_Element_Is_Reported()
    {
        var schema = Theo.Collection(Theo.String().Email());

        var result = schema.SafeParse(new[] { "bad", "a@b.co", "worse" });

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(new[] { "[0]", "[2]" }, result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void Nested_Inside_An_Object_Produces_A_Full_Path()
    {
        var schema = Theo.Object<Mailing>()
            .Field(x => x.Recipients, Theo.Collection(Theo.String().Email()));

        var value = new Mailing { Recipients = ["ok@example.com", "nope"] };

        var result = schema.SafeParse(value);

        Assert.Equal("Recipients[1]", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void An_Object_Inside_A_Collection_Produces_The_Whole_Path()
    {
        var schema = Theo.Collection(
            Theo.Object<Address>().Field(a => a.ZipCode, Theo.String().Length(8)));

        var result = schema.SafeParse(new[]
        {
            new Address { ZipCode = "12345678" },
            new Address { ZipCode = "bad" },
        });

        Assert.Equal("[1].ZipCode", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Count_Rules_Report_Against_The_Collection()
    {
        var result = Theo.Collection(Theo.String()).MinCount(2).SafeParse(new[] { "only one" });

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
        Assert.Equal(ValidationOrigin.Collection, error.Origin);
        Assert.True(error.Path.IsRoot);
        Assert.Equal("Must contain at least 2 item(s).", error.Message);
    }

    [Fact]
    public void MaxCount_And_Count_And_NotEmpty()
    {
        Assert.False(Theo.Collection(Theo.String()).MaxCount(1).IsValid(new[] { "a", "b" }));
        Assert.True(Theo.Collection(Theo.String()).Count(2).IsValid(new[] { "a", "b" }));
        Assert.False(Theo.Collection(Theo.String()).Count(2).IsValid(new[] { "a" }));
        Assert.False(Theo.Collection(Theo.String()).NotEmpty().IsValid(Array.Empty<string>()));
    }

    [Fact]
    public void A_Wrong_Count_Suppresses_The_Element_Errors()
    {
        // The list is the wrong length; saying so once beats saying so and then listing every
        // element that also happens to be invalid.
        var schema = Theo.Collection(Theo.String().Email()).MinCount(5);

        var result = schema.SafeParse(new[] { "bad", "also bad" });

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
    }

    [Fact]
    public void Null_Fails_As_An_Invalid_Type()
    {
        var result = Theo.Collection(Theo.String()).SafeParse(null!);

        Assert.Equal(ValidationErrorCode.InvalidType, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void AllowNull_Accepts_Null()
    {
        var schema = Theo.Collection(Theo.String()).NotEmpty().AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.False(schema.SafeParse(Array.Empty<string>()).IsSuccess);
    }

    [Fact]
    public void Works_With_List_And_Array_Alike()
    {
        var schema = Theo.Collection(Theo.Int().Min(1));

        Assert.True(schema.IsValid(new List<int> { 1, 2 }));
        Assert.True(schema.IsValid(new[] { 1, 2 }));
        Assert.False(schema.IsValid(new List<int> { 0 }));
    }

    [Fact]
    public void An_Empty_List_Passes_When_No_Count_Rule_Applies() =>
        Assert.True(Theo.Collection(Theo.String().Email()).IsValid(Array.Empty<string>()));
}
