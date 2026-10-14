using System.Text.Json;
using System.Text.Json.Nodes;
using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// The names a caller sees, in their own spelling.
/// </summary>
/// <remarks>
/// Error paths and document properties both came out as the C# member name, which is the one name
/// nobody on the wire uses. An application serializing camelCase got <c>ZipCode</c> in the error it
/// returned and <c>zipCode</c> in the body it received, so a form could not key one by the other.
/// </remarks>
[CollectionDefinition(nameof(NamingPolicyTests), DisableParallelization = true)]
public class SerializedByGlobalState;

// Sets SchemaGlobalOptions, which is process-wide, so it cannot run beside anything that renders a
// path. The same reason the one other test touching a global lives in an assembly of its own.
[Collection(nameof(NamingPolicyTests))]
public class NamingPolicyTests
{
    private static readonly ParseOptions CamelCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSchemaOptions CamelCaseDocument =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Schema<Address> AddressSchema =>
        Theo.Object<Address>()
            .Field(x => x.Street, Theo.String().MinLength(3))
            .Field(x => x.ZipCode, Theo.String().Length(8));

    [Fact]
    public void An_Error_Path_Takes_The_Policy()
    {
        var result = AddressSchema.SafeParse(new Address { Street = "ok street", ZipCode = "1" }, CamelCase);

        Assert.Equal("zipCode", Assert.Single(result.Errors).Path.ToString());
    }

    // The default is nobody's policy, which is what every release so far produced.
    [Fact]
    public void Without_One_The_Name_Is_The_One_The_Property_Has()
    {
        var result = AddressSchema.SafeParse(new Address { Street = "ok street", ZipCode = "1" });

        Assert.Equal("ZipCode", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void A_Nested_Path_Takes_It_At_Every_Level()
    {
        var schema = Theo.Object<Comment>()
            .Field(x => x.Replies, Theo.Collection(
                Theo.Object<Comment>().Field(x => x.Body, Theo.String().MinLength(3))));

        var value = new Comment { Replies = [new Comment { Body = "x" }] };

        var result = schema.SafeParse(value, CamelCase);

        Assert.Equal("replies[0].body", Assert.Single(result.Errors).Path.ToString());
    }

    // An index is a position and not a name, so there is nothing for a policy to do to it.
    [Fact]
    public void An_Index_Is_Left_Alone()
    {
        var schema = Theo.Collection(Theo.String().MinLength(3));

        var result = schema.SafeParse(["okay", "x"], CamelCase);

        Assert.Equal("[1]", Assert.Single(result.Errors).Path.ToString());
    }

    // Every rendering reads the same segments, so none of them needs telling about the policy.
    [Fact]
    public void Every_Rendering_Agrees()
    {
        var result = AddressSchema.SafeParse(new Address { Street = "x", ZipCode = "1" }, CamelCase);

        Assert.Equal(["street", "zipCode"], result.Errors.Flatten().ToDictionary().Keys);
        Assert.Contains("zipCode:", result.Errors.ToPrettyString(), StringComparison.Ordinal);
        Assert.Equal(
            ["street", "zipCode"],
            result.Errors.ToTree().Properties.Keys);
    }

    [Fact]
    public void A_Policy_On_The_Call_Beats_The_Global_One()
    {
        var previous = SchemaGlobalOptions.PropertyNamingPolicy;

        try
        {
            SchemaGlobalOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

            var withCall = AddressSchema.SafeParse(new Address { Street = "ok street", ZipCode = "1" }, CamelCase);
            Assert.Equal("zipCode", Assert.Single(withCall.Errors).Path.ToString());

            var withGlobal = AddressSchema.SafeParse(new Address { Street = "ok street", ZipCode = "1" });
            Assert.Equal("zip_code", Assert.Single(withGlobal.Errors).Path.ToString());
        }
        finally
        {
            SchemaGlobalOptions.PropertyNamingPolicy = previous;
        }
    }

    [Fact]
    public void The_Document_Takes_The_Policy()
    {
        var document = AddressSchema.ToJsonSchema(CamelCaseDocument);

        Assert.Equal(
            ["street", "zipCode"],
            document["properties"]!.AsObject().Select(static pair => pair.Key));

        Assert.Equal(
            ["street", "zipCode"],
            document["required"]!.AsArray().Select(static node => node!.GetValue<string>()));
    }

    [Fact]
    public void Without_One_The_Document_Says_The_Property_Name()
    {
        var document = AddressSchema.ToJsonSchema();

        Assert.Equal(
            ["Street", "ZipCode"],
            document["properties"]!.AsObject().Select(static pair => pair.Key));
    }

    [Fact]
    public void A_Nested_Document_Takes_It_At_Every_Level()
    {
        var schema = Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String())
            .Field(x => x.Replies, Theo.Collection(
                Theo.Object<Comment>().Field(x => x.Body, Theo.String())));

        var document = schema.ToJsonSchema(CamelCaseDocument);

        Assert.NotNull(document["properties"]!["body"]);
        Assert.NotNull(document["properties"]!["replies"]!["items"]!["properties"]!["body"]);
    }

    // A custom schema reads it off the context, which is where Direction already is.
    [Fact]
    public void A_Custom_Schema_Can_Read_It()
    {
        var document = AddressSchema.Describe(
            policy: JsonNamingPolicy.CamelCase).Root.Properties!;

        Assert.Equal(["street", "zipCode"], document.Select(static p => p.Name));
    }

    [Fact]
    public void A_Camel_Case_Document_Survives_The_Round_Trip()
    {
        var document = AddressSchema.ToJsonSchema(CamelCaseDocument);

        var again = Theo.JsonSchema(document).ToJsonSchema();

        Assert.True(
            JsonNode.DeepEquals(document, again),
            "out:  " + document.ToJsonString() + " back: " + again.ToJsonString());
    }
}
