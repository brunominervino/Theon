using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// An object schema accepts value types too: structs, record structs and readonly record structs.
/// </summary>
public class StructObjectSchemaTests
{
    [Fact]
    public void A_Plain_Struct_Validates()
    {
        var schema = Theo.Object<Coordinates>()
            .Field(x => x.Latitude, Theo.Double().Min(-90).Max(90))
            .Field(x => x.Longitude, Theo.Double().Min(-180).Max(180));

        Assert.True(schema.IsValid(new Coordinates { Latitude = -23.5, Longitude = -46.6 }));
        Assert.False(schema.IsValid(new Coordinates { Latitude = 100, Longitude = 0 }));
    }

    [Fact]
    public void The_Error_Path_Names_The_Property()
    {
        var schema = Theo.Object<Coordinates>()
            .Field(x => x.Latitude, Theo.Double().Min(-90).Max(90));

        var result = schema.SafeParse(new Coordinates { Latitude = 100 });

        Assert.Equal("Latitude", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void A_Record_Struct_Validates()
    {
        var schema = Theo.Object<Money>()
            .Field(x => x.Amount, Theo.Decimal().Positive())
            .Field(x => x.Currency, Theo.String().Length(3).Uppercase());

        Assert.True(schema.IsValid(new Money(10.50m, "BRL")));

        var result = schema.SafeParse(new Money(-1m, "brl"));

        Assert.Equal(
            new[] { "Amount", "Currency" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void A_Readonly_Record_Struct_Validates()
    {
        var schema = Theo.Object<Temperature>()
            .Field(x => x.Celsius, Theo.Double().Min(-273.15));

        Assert.True(schema.IsValid(new Temperature(21.5)));
        Assert.False(schema.IsValid(new Temperature(-300)));
    }

    [Fact]
    public void A_Default_Struct_Is_Validated_Like_Any_Other()
    {
        // default(Money) has a null Currency and a zero Amount. Nothing about being a struct
        // exempts it from the rules; this is exactly the case that would slip through untyped.
        var schema = Theo.Object<Money>()
            .Field(x => x.Amount, Theo.Decimal().Positive())
            .Field(x => x.Currency, Theo.String().Length(3));

        var result = schema.SafeParse(default);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(ValidationErrorCode.InvalidType, result.Errors[1].Code);
    }

    [Fact]
    public void A_Struct_Nests_Inside_A_Class()
    {
        var schema = Theo.Object<Reading>()
            .Field(x => x.Where, Theo.Object<Coordinates>()
                .Field(c => c.Latitude, Theo.Double().Min(-90).Max(90)));

        var result = schema.SafeParse(new Reading { Where = new Coordinates { Latitude = 999 } });

        Assert.Equal("Where.Latitude", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void AllowNull_On_A_Struct_Schema_Produces_A_Nullable()
    {
        Schema<Temperature?> schema = Theo.Object<Temperature>()
            .Field(x => x.Celsius, Theo.Double().Min(-273.15))
            .AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.True(schema.SafeParse(new Temperature(20)).IsSuccess);
        Assert.False(schema.SafeParse(new Temperature(-500)).IsSuccess);
    }

    [Fact]
    public void A_Nullable_Struct_Nests_Inside_A_Class()
    {
        var schema = Theo.Object<Reading>()
            .Field(x => x.Value, Theo.Object<Temperature>()
                .Field(t => t.Celsius, Theo.Double().Min(-273.15))
                .AllowNull());

        Assert.True(schema.SafeParse(new Reading { Value = null }).IsSuccess);
        Assert.Equal(
            "Value.Celsius",
            Assert.Single(schema.SafeParse(new Reading { Value = new Temperature(-400) }).Errors)
                .Path.ToString());
    }

    [Fact]
    public void AllowNull_Still_Reads_As_AllowNull_For_A_Class()
    {
        // The reference overload has to keep resolving: two extension methods, picked by
        // constraint, both spelled the same at the call site.
        Schema<Address?> schema = Theo.Object<Address>()
            .Field(a => a.ZipCode, Theo.String().Length(8))
            .AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.False(schema.SafeParse(new Address { ZipCode = "no" }).IsSuccess);
    }

    [Fact]
    public void A_Cross_Field_Rule_Works_On_A_Record_Struct()
    {
        var schema = Theo.Object<Span>()
            .Field(x => x.Start, Theo.DateOnly())
            .Field(x => x.End, Theo.DateOnly())
            .Refine(x => x.End >= x.Start, x => x.End, "The end cannot precede the start.");

        var ok = new Span(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));
        var backwards = new Span(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1));

        Assert.True(schema.IsValid(ok));

        var error = Assert.Single(schema.SafeParse(backwards).Errors);
        Assert.Equal("End", error.Path.ToString());
    }

    [Fact]
    public void A_Collection_Of_Structs_Reports_By_Index()
    {
        var schema = Theo.Collection(
            Theo.Object<Money>().Field(m => m.Amount, Theo.Decimal().Positive()));

        var result = schema.SafeParse(new[] { new Money(1m, "BRL"), new Money(-1m, "BRL") });

        Assert.Equal("[1].Amount", Assert.Single(result.Errors).Path.ToString());
    }
}
