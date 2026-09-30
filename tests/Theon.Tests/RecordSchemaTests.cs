using Theon.Errors;
using Theon.Schemas;

namespace Theon.Tests;

/// <summary>
/// Dictionaries, whose keys are data rather than structure.
/// </summary>
public class RecordSchemaTests
{
    private static readonly RecordSchema<string, decimal> Prices =
        Theo.Record(Theo.String().Length(3).Uppercase(), Theo.Decimal().Positive());

    private static Dictionary<string, decimal> Dict(params (string Key, decimal Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);

    [Fact]
    public void A_Valid_Dictionary_Passes() =>
        Assert.True(Prices.IsValid(Dict(("BRL", 10.5m), ("USD", 2m))));

    [Fact]
    public void A_Bad_Value_Is_Reported_Under_Its_Key()
    {
        var result = Prices.SafeParse(Dict(("BRL", 10m), ("USD", -1m)));

        var error = Assert.Single(result.Errors);
        Assert.Equal("USD", error.Path.ToString());
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
    }

    [Fact]
    public void A_Bad_Key_Is_Reported_Under_Itself()
    {
        var result = Prices.SafeParse(Dict(("brl", 10m)));

        Assert.Contains(result.Errors, e => e.Path.ToString() == "brl");
    }

    [Fact]
    public void Every_Bad_Entry_Is_Reported()
    {
        var result = Prices.SafeParse(Dict(("BRL", -1m), ("USD", 2m), ("EUR", -3m)));

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(new[] { "BRL", "EUR" }, result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void Count_Rules_Report_Against_The_Dictionary()
    {
        var result = Prices.MinCount(2).SafeParse(Dict(("BRL", 1m)));

        var error = Assert.Single(result.Errors);
        Assert.True(error.Path.IsRoot);
        Assert.Equal(ValidationOrigin.Collection, error.Origin);
    }

    [Fact]
    public void MaxCount_And_NotEmpty()
    {
        Assert.False(Prices.MaxCount(1).IsValid(Dict(("BRL", 1m), ("USD", 2m))));
        Assert.False(Prices.NotEmpty().IsValid(Dict()));
        Assert.True(Prices.IsValid(Dict()));
    }

    [Fact]
    public void The_String_Keyed_Overload_Leaves_Keys_Alone()
    {
        var schema = Theo.Record(Theo.Int().Min(0));

        Assert.True(schema.IsValid(new Dictionary<string, int> { ["anything at all"] = 1 }));
        Assert.False(schema.IsValid(new Dictionary<string, int> { ["k"] = -1 }));
    }

    [Fact]
    public void Nested_Inside_An_Object_Produces_A_Full_Path()
    {
        var schema = Theo.Object<Catalogue>()
            .Field(x => x.Prices, Theo.Record(Theo.Decimal().Positive()));

        var value = new Catalogue { Prices = new Dictionary<string, decimal> { ["BRL"] = -1m } };

        Assert.Equal("Prices.BRL", Assert.Single(schema.SafeParse(value).Errors).Path.ToString());
    }

    [Fact]
    public void Null_Fails_As_An_Invalid_Type() =>
        Assert.Equal(
            ValidationErrorCode.InvalidType,
            Assert.Single(Prices.SafeParse(null!).Errors).Code);

    [Fact]
    public void Objects_Can_Be_Values()
    {
        var schema = Theo.Record(Theo.Object<Address>().Field(a => a.ZipCode, Theo.String().Length(8)));

        var value = new Dictionary<string, Address> { ["home"] = new() { ZipCode = "bad" } };

        Assert.Equal("home.ZipCode", Assert.Single(schema.SafeParse(value).Errors).Path.ToString());
    }

    [Fact]
    public async Task Works_On_The_Asynchronous_Path()
    {
        var schema = Theo.Record(
            Theo.String().RefineAsync((v, _) => ValueTask.FromResult(v != "banned"), "Not allowed."));

        var value = new Dictionary<string, string> { ["a"] = "fine", ["b"] = "banned" };

        var result = await schema.SafeParseAsync(value, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("b", Assert.Single(result.Errors).Path.ToString());
    }
}
