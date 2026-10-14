using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// The schema for a <see cref="bool"/>.
/// </summary>
/// <remarks>
/// <para>
/// It was left out on the argument that a <see cref="bool"/> has nothing to validate, which is true
/// of the value and false of everything around it. Without a <c>Schema&lt;bool&gt;</c> a boolean
/// property cannot be bound to a field at all, so an object holding one cannot be described: the
/// generated document simply has no entry for it, and a client generated from that document does not
/// know the field exists.
/// </para>
/// <para>
/// There was a way to say it — <c>Theo.OneOf(Theo.Literal(true), Theo.Literal(false))</c> — and it
/// produces <c>anyOf: [{const: true}, {const: false}]</c>, which is a correct and unrecognisable
/// spelling of <c>{"type": "boolean"}</c>.
/// </para>
/// </remarks>
public class BooleanSchemaTests
{
    [Fact]
    public void Both_Values_Are_Accepted()
    {
        var schema = Theo.Boolean();

        Assert.True(schema.SafeParse(true).IsSuccess);
        Assert.True(schema.SafeParse(false).IsSuccess);
    }

    [Fact]
    public void The_Document_Says_Boolean()
    {
        var document = Theo.Boolean().ToJsonSchema();

        Assert.Equal("boolean", document["type"]!.GetValue<string>());
    }

    // The reason this exists. A boolean property had no schema to bind it to, so it was absent from
    // every document generated for an object that had one.
    [Fact]
    public void A_Boolean_Property_Reaches_The_Document()
    {
        var document = Theo.Object<Consent>()
            .Field(x => x.Accepted, Theo.Boolean())
            .Field(x => x.Newsletter, Theo.Boolean().AllowNull())
            .ToJsonSchema();

        var properties = document["properties"]!;

        Assert.Equal("boolean", properties["Accepted"]!["type"]!.GetValue<string>());
        Assert.Equal(
            ["boolean", "null"],
            properties["Newsletter"]!["type"]!.AsArray().Select(static node => node!.GetValue<string>()));

        // Required is derived from refusing null, as it is for every other kind.
        Assert.Equal(
            ["Accepted"],
            document["required"]!.AsArray().Select(static node => node!.GetValue<string>()));
    }

    [Fact]
    public void AllowNull_Accepts_Null_And_A_Value()
    {
        var schema = Theo.Boolean().AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.True(schema.SafeParse(true).IsSuccess);
    }

    [Fact]
    public void Required_Refuses_Null()
    {
        var result = Theo.Boolean().Required().SafeParse(null);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("A value is required.", error.Message);
    }

    [Fact]
    public void Default_Substitutes_For_Null()
    {
        var schema = Theo.Boolean().Default(true);

        Assert.True(schema.Parse(null));
        Assert.False(schema.Parse(false));
    }

    [Fact]
    public void A_Default_Reaches_The_Document()
    {
        var document = Theo.Boolean().Default(true).ToJsonSchema();

        Assert.Equal(
            ["boolean", "null"],
            document["type"]!.AsArray().Select(static node => node!.GetValue<string>()));
        Assert.True(document["default"]!.GetValue<bool>());
    }

    [Fact]
    public void A_Refinement_Composes()
    {
        var schema = Theo.Boolean().Refine(static value => value, "You have to agree to continue.");

        Assert.True(schema.SafeParse(true).IsSuccess);
        Assert.Equal(
            "You have to agree to continue.",
            Assert.Single(schema.SafeParse(false).Errors).Message);
    }

    // Demanding one particular value is what Literal is for, and this does not become a second way to
    // say it. The test is here so that nobody adds one.
    [Fact]
    public void Demanding_True_Is_Still_Literals_Job()
    {
        var document = Theo.Literal(true).ToJsonSchema();

        Assert.True(document["const"]!.GetValue<bool>());
    }

    [Fact]
    public void A_Boolean_Document_Survives_The_Round_Trip()
    {
        var document = Theo.Object<Consent>()
            .Field(x => x.Accepted, Theo.Boolean())
            .ToJsonSchema();

        var again = Theo.JsonSchema(document).ToJsonSchema();

        Assert.True(
            System.Text.Json.Nodes.JsonNode.DeepEquals(document, again),
            "out:  " + document.ToJsonString() + " back: " + again.ToJsonString());
    }

    [Fact]
    public void Refine_Refuses_A_Missing_Predicate() =>
        Assert.Throws<ArgumentNullException>(() => Theo.Boolean().Refine(null!, "x"));
}
