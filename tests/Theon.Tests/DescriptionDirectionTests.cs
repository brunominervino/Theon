namespace Theon.Tests;

/// <summary>
/// Which side of a schema a document describes.
/// </summary>
/// <remarks>
/// <para>
/// A schema that transforms has two shapes, and a document can only show one of them. Describing the
/// input is right for a request body, which is what the caller sends; describing the output is right
/// for a response body, which is what the schema produced.
/// </para>
/// <para>
/// These are written out rather than reasoned about because the difference between the two sides is
/// easy to get backwards one wrapper at a time, and a document that describes the wrong side is
/// wrong in a way nothing else in the library would notice.
/// </para>
/// </remarks>
public class DescriptionDirectionTests
{
    private static readonly JsonSchemaOptions Output = new() { Direction = DescriptionDirection.Output };

    private static readonly JsonSchemaOptions Strict = new()
    {
        Direction = DescriptionDirection.Output,
        OnUnrepresentable = UnrepresentablePolicy.Throw,
    };

    // The default has to stay the input side: every document generated before this option existed
    // described the input, and changing that silently would rewrite every caller's OpenAPI document.
    [Fact]
    public void The_Default_Direction_Is_The_Input_Side()
    {
        var schema = Theo.String().Transform(static text => text.Length, Theo.Int().Min(1));

        var byDefault = schema.ToJsonSchema();
        var explicitly = schema.ToJsonSchema(new JsonSchemaOptions
        {
            Direction = DescriptionDirection.Input,
        });

        Assert.Equal(explicitly.ToJsonString(), byDefault.ToJsonString());
        Assert.Equal("string", byDefault["type"]!.GetValue<string>());
    }

    [Fact]
    public void A_Transform_With_A_Follow_On_Schema_Describes_That_Schema_On_The_Output_Side()
    {
        var schema = Theo.String().MinLength(1).Transform(static text => text.Length, Theo.Int().Min(1).Max(99));

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal("string", input["type"]!.GetValue<string>());
        Assert.Equal(1, input["minLength"]!.GetValue<int>());

        Assert.Equal("integer", output["type"]!.GetValue<string>());
        Assert.Equal(1, output["minimum"]!.GetValue<int>());
        Assert.Equal(99, output["maximum"]!.GetValue<int>());
    }

    // The output side of a transform that has a schema for its result is fully describable, which the
    // input side never is: nothing is left out, so the strictest policy has nothing to complain about.
    [Fact]
    public void A_Transform_With_A_Follow_On_Schema_Leaves_Nothing_Out_On_The_Output_Side()
    {
        var schema = Theo.String().Transform(static text => text.Length, Theo.Int().Min(1));

        var output = schema.ToJsonSchema(Strict);

        Assert.Equal("integer", output["type"]!.GetValue<string>());

        Assert.Throws<InvalidOperationException>(() => schema.ToJsonSchema(new JsonSchemaOptions
        {
            OnUnrepresentable = UnrepresentablePolicy.Throw,
        }));
    }

    // The price of handing over a function instead of a schema. The type is all there is to say about
    // what comes out, and the type is not a schema.
    [Fact]
    public void A_Transform_Without_A_Follow_On_Schema_Describes_Only_The_Type_It_Produces()
    {
        var schema = Theo.String().MinLength(3).Transform(static text => text.Length);

        var output = schema.ToJsonSchema(Output);

        Assert.Equal("integer", output["type"]!.GetValue<string>());
        Assert.Null(output["minLength"]);
        Assert.Null(output["minimum"]);
    }

    [Fact]
    public void A_Transform_Without_A_Follow_On_Schema_Reports_That_It_Had_Nothing_To_Describe()
    {
        var schema = Theo.String().Transform(static text => text.Length);

        var exception = Assert.Throws<InvalidOperationException>(() => schema.ToJsonSchema(Strict));

        Assert.Contains("Transform", exception.Message, StringComparison.Ordinal);
    }

    // A transform to a type that travels as nothing in particular says nothing at all, rather than
    // guessing a type. That is the same answer the base description gives for a schema it does not
    // know, and for the same reason.
    [Fact]
    public void A_Transform_To_A_Type_With_No_Json_Shape_Describes_Nothing()
    {
        var schema = Theo.String().Transform(static text => new Address { Street = text, ZipCode = text });

        var output = schema.ToJsonSchema(Output);

        Assert.Null(output["type"]);
    }

    [Fact]
    public void A_TryTransform_Describes_The_Converted_Side_On_Output()
    {
        var schema = Theo.String().Trim()
            .TryTransform<int>(int.TryParse, "Must be a whole number.", Theo.Int().Min(1).Max(100));

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal("string", input["type"]!.GetValue<string>());
        Assert.Equal("integer", output["type"]!.GetValue<string>());
        Assert.Equal(100, output["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void A_TryTransform_Without_A_Follow_On_Schema_Describes_Only_The_Type()
    {
        var schema = Theo.String().TryTransform<int>(int.TryParse, "Must be a whole number.");

        var output = schema.ToJsonSchema(Output);

        Assert.Equal("integer", output["type"]!.GetValue<string>());
        Assert.Throws<InvalidOperationException>(() => schema.ToJsonSchema(Strict));
    }

    // Default is the clearest case of the two sides differing: null goes in, and a value always comes
    // out. "default" is an annotation about what happens to an absent input, so it belongs on the
    // input side and means nothing on the output side.
    [Fact]
    public void A_Default_Accepts_Null_On_Input_And_Never_Produces_It()
    {
        var schema = Theo.Int().Min(1).Default(10);

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(
            ["integer", "null"],
            input["type"]!.AsArray().Select(static node => node!.GetValue<string>()));
        Assert.Equal(10, input["default"]!.GetValue<int>());

        Assert.Equal("integer", output["type"]!.GetValue<string>());
        Assert.Null(output["default"]);
    }

    [Fact]
    public void A_Default_On_A_Reference_Type_Behaves_The_Same_Way()
    {
        var schema = Theo.String().MinLength(1).Default("anonymous");

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(
            ["string", "null"],
            input["type"]!.AsArray().Select(static node => node!.GetValue<string>()));
        Assert.Equal("anonymous", input["default"]!.GetValue<string>());

        Assert.Equal("string", output["type"]!.GetValue<string>());
        Assert.Null(output["default"]);
    }

    // Required is the one that looks like it should differ and does not. Its declared output type is
    // nullable, so the reflex is to say the output may be null -- but a null output only happens when
    // the parse failed, and a description describes the values a parse succeeds with.
    [Fact]
    public void Required_Is_The_Same_On_Both_Sides_Because_Null_Never_Survives_It()
    {
        var schema = Theo.Int().Min(1).Required();

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(input.ToJsonString(), output.ToJsonString());
        Assert.Equal("integer", output["type"]!.GetValue<string>());
    }

    [Fact]
    public void Required_On_A_Reference_Type_Is_Also_The_Same_On_Both_Sides()
    {
        var schema = Theo.String().MinLength(1).Required();

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(input.ToJsonString(), output.ToJsonString());
        Assert.Equal("string", output["type"]!.GetValue<string>());
    }

    [Fact]
    public void AllowNull_Permits_Null_On_Both_Sides()
    {
        var schema = Theo.Int().Min(1).AllowNull();

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(input.ToJsonString(), output.ToJsonString());
        Assert.Equal(
            ["integer", "null"],
            output["type"]!.AsArray().Select(static node => node!.GetValue<string>()));
    }

    [Fact]
    public void AllowNull_On_A_Reference_Type_Permits_Null_On_Both_Sides()
    {
        var schema = Theo.String().MinLength(1).AllowNull();

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal(input.ToJsonString(), output.ToJsonString());
        Assert.Equal(
            ["string", "null"],
            output["type"]!.AsArray().Select(static node => node!.GetValue<string>()));
    }

    // Catch is the case where the output side is the more precise of the two. The input side shows the
    // shape worth aiming for; the output side has to admit the fallback, because a fallback the inner
    // schema would reject is exactly the interesting case.
    [Fact]
    public void A_Catch_Admits_Its_Fallback_On_The_Output_Side()
    {
        var schema = Theo.String().Email().Catch("nobody@example.com");

        var input = schema.ToJsonSchema();
        var output = schema.ToJsonSchema(Output);

        Assert.Equal("string", input["type"]!.GetValue<string>());
        Assert.Equal("email", input["format"]!.GetValue<string>());
        Assert.Equal("nobody@example.com", input["default"]!.GetValue<string>());

        var branches = output["anyOf"]!.AsArray();
        Assert.Equal(2, branches.Count);
        Assert.Equal("email", branches[0]!["format"]!.GetValue<string>());
        Assert.Equal("nobody@example.com", branches[1]!["const"]!.GetValue<string>());
        Assert.Null(output["default"]);
    }

    // The direction has to reach the children, or it describes the outside of an object and the inside
    // of nothing. It travels on the context rather than as an argument for exactly this reason.
    [Fact]
    public void The_Direction_Reaches_An_Objects_Fields()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().Transform(static text => text.Trim(), Theo.String().MinLength(2)))
            .Field(x => x.Email, Theo.String().Email());

        var output = schema.ToJsonSchema(Output);

        var name = output["properties"]!["Name"]!;
        Assert.Equal("string", name["type"]!.GetValue<string>());
        Assert.Equal(2, name["minLength"]!.GetValue<int>());
    }

    [Fact]
    public void The_Direction_Reaches_A_Collections_Items()
    {
        var schema = Theo.Collection(
            Theo.String().Transform(static text => text.Trim(), Theo.String().MinLength(4)));

        var output = schema.ToJsonSchema(Output);

        Assert.Equal(4, output["items"]!["minLength"]!.GetValue<int>());
    }

    [Fact]
    public void The_Direction_Reaches_A_Record_Value()
    {
        var schema = Theo.Record(
            Theo.String().Transform(static text => text.Trim(), Theo.String().MinLength(5)));

        var output = schema.ToJsonSchema(Output);

        Assert.Equal(5, output["additionalProperties"]!["minLength"]!.GetValue<int>());
    }
}
