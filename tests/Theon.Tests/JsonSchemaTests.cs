using System.Text.Json.Nodes;

namespace Theon.Tests;

/// <summary>
/// Schemas describing themselves as JSON Schema 2020-12.
/// </summary>
public class JsonSchemaTests
{
    private static readonly Schema<Comment> CommentSchema = BuildCommentSchema();

    private static JsonObject Schema<TIn, TOut>(Schema<TIn, TOut> schema) =>
        schema.ToJsonSchema(new JsonSchemaOptions { IncludeDialect = false });

    private static string Json<TIn, TOut>(Schema<TIn, TOut> schema) => Schema(schema).ToJsonString();

    [Fact]
    public void The_Document_Declares_Its_Dialect()
    {
        var document = Theo.String().ToJsonSchema();

        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            document["$schema"]!.GetValue<string>());
    }

    [Fact]
    public void A_Title_Goes_On_The_Document()
    {
        var document = Theo.String().ToJsonSchema(new JsonSchemaOptions { Title = "Name" });

        Assert.Equal("Name", document["title"]!.GetValue<string>());
    }

    [Fact]
    public void String_Length_Becomes_MinLength_And_MaxLength() =>
        Assert.Equal(
            """{"type":"string","minLength":3,"maxLength":100}""",
            Json(Theo.String().MinLength(3).MaxLength(100)));

    [Fact]
    public void An_Exact_Length_Becomes_Both_Bounds() =>
        Assert.Equal(
            """{"type":"string","minLength":8,"maxLength":8}""",
            Json(Theo.String().Length(8)));

    // A format this library names and the dialect also names becomes that format. Anything else
    // becomes the pattern itself, which needs no agreement between the two vocabularies.
    [Theory]
    [InlineData("email")]
    [InlineData("uri")]
    [InlineData("uuid")]
    public void A_Shared_Format_Name_Becomes_A_Format(string expected)
    {
        var schema = expected switch
        {
            "email" => Theo.String().Email(),
            "uri" => Theo.String().Url(),
            _ => Theo.String().Uuid(),
        };

        var document = Schema(schema);

        Assert.Equal(expected, document["format"]!.GetValue<string>());
        Assert.Null(document["pattern"]);
    }

    [Fact]
    public void A_Format_Only_This_Library_Names_Becomes_A_Pattern()
    {
        var document = Schema(Theo.String().Hex());

        Assert.Null(document["format"]);
        Assert.Contains("0-9A-Fa-f", document["pattern"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Iso8601_Rules_Become_Date_Formats()
    {
        Assert.Equal("date-time", Schema(Theo.String().Iso8601())["format"]!.GetValue<string>());
        Assert.Equal("date", Schema(Theo.String().Iso8601Date())["format"]!.GetValue<string>());
    }

    [Fact]
    public void An_Integer_And_A_Number_Are_Told_Apart_By_Their_Type()
    {
        Assert.Equal("integer", Schema(Theo.Int())["type"]!.GetValue<string>());
        Assert.Equal("integer", Schema(Theo.Long())["type"]!.GetValue<string>());
        Assert.Equal("number", Schema(Theo.Decimal())["type"]!.GetValue<string>());
        Assert.Equal("number", Schema(Theo.Double())["type"]!.GetValue<string>());
    }

    [Fact]
    public void Numeric_Bounds_Distinguish_Inclusive_From_Exclusive()
    {
        Assert.Equal(
            """{"type":"integer","minimum":18,"maximum":120}""",
            Json(Theo.Int().Min(18).Max(120)));

        Assert.Equal(
            """{"type":"integer","exclusiveMinimum":0,"exclusiveMaximum":10}""",
            Json(Theo.Int().GreaterThan(0).LessThan(10)));
    }

    [Fact]
    public void MultipleOf_Survives() =>
        Assert.Equal("""{"type":"integer","multipleOf":5}""", Json(Theo.Int().MultipleOf(5)));

    [Fact]
    public void A_Guid_Is_A_String_With_A_Uuid_Format() =>
        Assert.Equal("""{"type":"string","format":"uuid"}""", Json(Theo.Guid()));

    [Fact]
    public void The_Temporal_Schemas_Carry_Their_Formats()
    {
        Assert.Equal("date-time", Schema(Theo.DateTime())["format"]!.GetValue<string>());
        Assert.Equal("date-time", Schema(Theo.DateTimeOffset())["format"]!.GetValue<string>());
        Assert.Equal("date", Schema(Theo.DateOnly())["format"]!.GetValue<string>());
        Assert.Equal("time", Schema(Theo.TimeOnly())["format"]!.GetValue<string>());
    }

    // minimum and maximum are numeric keywords, and a validator ignores them on a string. So a date
    // range is left out rather than written where it cannot apply: a document that omits a rule is
    // incomplete, one that states a rule nothing enforces is wrong.
    [Fact]
    public void A_Temporal_Bound_Is_Left_Out_Rather_Than_Written_Where_It_Cannot_Apply()
    {
        var document = Schema(Theo.DateOnly().Min(new DateOnly(2026, 1, 1)));

        Assert.Null(document["minimum"]);
        Assert.Equal("""{"type":"string","format":"date"}""", document.ToJsonString());
    }

    // A TimeSpan is a string, and not the dialect's "duration" format: .NET writes "00:00:05" where
    // an ISO 8601 duration would be "PT5S", so claiming that format would be a lie.
    [Fact]
    public void A_Duration_Is_A_String_With_No_Format() =>
        Assert.Equal("""{"type":"string"}""", Json(Theo.TimeSpan().Positive()));

    [Fact]
    public void A_Uri_Is_A_Reference_Until_It_Has_To_Be_Absolute()
    {
        Assert.Equal("uri-reference", Schema(Theo.Uri())["format"]!.GetValue<string>());
        Assert.Equal("uri", Schema(Theo.Uri().Absolute())["format"]!.GetValue<string>());
    }

    [Fact]
    public void An_Enum_Lists_Its_Member_Names() =>
        Assert.Equal(
            """{"type":"string","enum":["Pending","InProgress","Completed"]}""",
            Json(Theo.Enum<TaskStatus>()));

    [Fact]
    public void A_Literal_Becomes_A_Const()
    {
        Assert.Equal("""{"type":"string","const":"pix"}""", Json(Theo.Literal("pix")));
        Assert.Equal("""{"type":"integer","const":2}""", Json(Theo.Literal(2)));
    }

    [Fact]
    public void An_Object_Lists_Its_Properties()
    {
        var schema = Theo.Object<Address>()
            .Field(x => x.Street, Theo.String().MinLength(3))
            .Field(x => x.ZipCode, Theo.String().Length(8));

        Assert.Equal(
            """{"type":"object","properties":{"Street":{"type":"string","minLength":3},"ZipCode":{"type":"string","minLength":8,"maxLength":8}},"required":["Street","ZipCode"]}""",
            Json(schema));
    }

    // Required is derived, not declared: this library never asks whether a key was present, so the
    // only honest meaning is that the field refuses null.
    [Fact]
    public void A_Field_That_Accepts_Null_Is_Not_Required()
    {
        var schema = Theo.Object<Ticket>()
            .Field(x => x.Title, Theo.String().NotEmpty())
            .Field(x => x.ClosedBy, Theo.String().NotEmpty().AllowNull());

        var document = Schema(schema);

        Assert.Equal(
            ["Title"],
            document["required"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(
            """["string","null"]""",
            document["properties"]!["ClosedBy"]!["type"]!.ToJsonString());
    }

    [Fact]
    public void Required_Puts_The_Field_Back()
    {
        var schema = Theo.Object<Ticket>()
            .Field(x => x.CompletedAt, Theo.DateTime().Required());

        var document = Schema(schema);

        Assert.Equal(
            ["CompletedAt"],
            document["required"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    [Fact]
    public void A_Collection_Describes_Its_Elements() =>
        Assert.Equal(
            """{"type":"array","minItems":1,"maxItems":10,"items":{"type":"string","format":"email"}}""",
            Json(Theo.Collection(Theo.String().Email()).MinCount(1).MaxCount(10)));

    [Fact]
    public void Unique_Becomes_UniqueItems() =>
        Assert.Equal(
            """{"type":"array","uniqueItems":true,"items":{"type":"string"}}""",
            Json(Theo.Collection(Theo.String()).Unique()));

    // A set is distinct by construction, so it says so without being asked.
    [Fact]
    public void A_Set_Is_Unique_Without_A_Rule() =>
        Assert.Equal(
            """{"type":"array","maxItems":5,"uniqueItems":true,"items":{"type":"string"}}""",
            Json(Theo.Set(Theo.String()).MaxCount(5)));

    [Fact]
    public void A_Record_Becomes_An_Object_With_AdditionalProperties() =>
        Assert.Equal(
            """{"type":"object","additionalProperties":{"type":"number","exclusiveMinimum":0}}""",
            Json(Theo.Record(Theo.String(), Theo.Decimal().Positive())));

    [Fact]
    public void OneOf_Becomes_AnyOf()
    {
        var schema = Theo.OneOf("An address or a number.", Theo.String().Email(), Theo.String().E164());

        var document = Schema(schema);

        Assert.Equal(2, document["anyOf"]!.AsArray().Count);
        Assert.Equal("email", document["anyOf"]![0]!["format"]!.GetValue<string>());
    }

    [Fact]
    public void Subtypes_Becomes_AnyOf_Of_The_Branches()
    {
        var schema = Theo.Subtypes<Payment>()
            .Case(Theo.Object<PixPayment>().Field(x => x.Key, Theo.String().NotEmpty()))
            .Case(Theo.Object<CardPayment>().Field(x => x.Number, Theo.String().Length(16)));

        var document = Schema(schema);

        Assert.Equal(2, document["anyOf"]!.AsArray().Count);
        Assert.NotNull(document["anyOf"]![0]!["properties"]!["Key"]);
        Assert.NotNull(document["anyOf"]![1]!["properties"]!["Number"]);
    }

    [Fact]
    public void And_Becomes_AllOf()
    {
        var document = Schema(Theo.String().MaxLength(10).And(Theo.String().MinLength(2)));

        Assert.Equal(2, document["allOf"]!.AsArray().Count);
        Assert.Equal(10, document["allOf"]![0]!["maxLength"]!.GetValue<int>());
    }

    [Fact]
    public void A_Transform_Describes_What_The_Caller_Sends() =>
        Assert.Equal(
            """{"type":"string","minLength":1}""",
            Json(Theo.String().NotEmpty().Transform(int.Parse)));

    [Fact]
    public void Default_Says_The_Value_May_Be_Absent_And_What_It_Becomes() =>
        Assert.Equal(
            """{"type":["integer","null"],"minimum":1,"maximum":100,"default":20}""",
            Json(Theo.Int().Min(1).Max(100).Default(20)));

    [Fact]
    public void Catch_Records_Its_Fallback_As_The_Default() =>
        Assert.Equal(
            """{"type":"integer","minimum":1,"default":20}""",
            Json(Theo.Int().Min(1).Catch(20)));

    [Fact]
    public void Annotations_Reach_The_Document() =>
        Assert.Equal(
            """{"type":"string","format":"email","title":"E-mail","description":"Where we write.","examples":["ada@example.com"],"deprecated":true}""",
            Json(Theo.String().Email().Annotate(
                title: "E-mail",
                description: "Where we write.",
                example: "ada@example.com",
                deprecated: true)));

    [Fact]
    public void Annotating_With_Nothing_To_Say_Changes_Nothing()
    {
        var schema = Theo.String().Email();

        Assert.Same(schema, schema.Annotate());
    }

    [Fact]
    public void Annotations_Do_Not_Change_What_Is_Validated()
    {
        var schema = Theo.String().Email().Annotate(description: "Where we write.");

        Assert.True(schema.IsValid("ada@example.com"));
        Assert.False(schema.IsValid("nope"));
    }

    // What a Refine checks cannot be written down: an arbitrary predicate has no keyword. It is left
    // out rather than guessed at, and Annotate is how a reader is told about it.
    [Fact]
    public void A_Refinement_Adds_Nothing_And_Annotate_Is_The_Way_To_Say_It()
    {
        Assert.Equal(
            """{"type":"string","minLength":1}""",
            Json(Theo.String().NotEmpty().Refine(static v => v.StartsWith('a'), "Must start with a.")));

        Assert.Equal(
            """{"type":"string","minLength":1,"description":"Must start with the letter a."}""",
            Json(Theo.String().NotEmpty()
                .Refine(static v => v.StartsWith('a'), "Must start with a.")
                .Annotate(description: "Must start with the letter a.")));
    }

    // A normalization rewrites the value rather than constraining it, so there is nothing to say
    // about it either.
    [Fact]
    public void A_Normalization_Adds_Nothing() =>
        Assert.Equal(
            """{"type":"string","minLength":3}""",
            Json(Theo.String().Trim().ToLowerInvariant().MinLength(3)));

    // A schema that contains itself is written once under $defs and referred to from inside. Without
    // that the walk would never return.
    [Fact]
    public void A_Recursive_Schema_Becomes_A_Definition_And_A_Reference()
    {
        var document = CommentSchema.ToJsonSchema();

        Assert.Equal("#/$defs/Comment", document["$ref"]!.GetValue<string>());

        var definition = document["$defs"]!["Comment"]!;
        Assert.Equal("object", definition["type"]!.GetValue<string>());
        Assert.Equal(
            "#/$defs/Comment",
            definition["properties"]!["Replies"]!["items"]!["$ref"]!.GetValue<string>());
    }

    // The repetition has to be the same instance for there to be anything to recognise. A factory
    // that builds a fresh schema every call produces a new one at every level, and the generator says
    // so rather than running until the stack ends.
    [Fact]
    public void A_Lazy_Factory_That_Builds_A_New_Schema_Each_Time_Is_Refused()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => FreshEachTime().ToJsonSchema());

        Assert.Contains("Theo.Lazy", exception.Message, StringComparison.Ordinal);
    }

    // A custom schema from another assembly cannot describe itself, because the hook is internal. The
    // document says nothing about it rather than guessing a type.
    [Fact]
    public void A_Schema_That_Does_Not_Describe_Itself_Constrains_Nothing() =>
        Assert.Equal("{}", Json(new AlwaysValid()));

    [Fact]
    public void A_Whole_Request_Reads_As_One_Document()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Email().Annotate(description: "Where we write to you."))
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty());

        var document = schema.ToJsonSchema(new JsonSchemaOptions { Title = "CreateUserRequest" });

        Assert.Equal("CreateUserRequest", document["title"]!.GetValue<string>());
        Assert.Equal("object", document["type"]!.GetValue<string>());

        var properties = document["properties"]!;
        Assert.Equal(3, properties["Name"]!["minLength"]!.GetValue<int>());
        Assert.Equal("email", properties["Email"]!["format"]!.GetValue<string>());
        Assert.Equal(
            "Where we write to you.",
            properties["Email"]!["description"]!.GetValue<string>());
        Assert.Equal(18, properties["Age"]!["minimum"]!.GetValue<int>());
        Assert.Equal("uuid", properties["CompanyId"]!["format"]!.GetValue<string>());

        // NotEmpty on a Guid is a refinement, so there is no keyword for it and none is invented.
        Assert.Null(properties["CompanyId"]!["pattern"]);
    }

    [Fact]
    public void The_Document_Is_Mutable_So_A_Caller_Can_Add_To_It()
    {
        var document = Theo.String().ToJsonSchema();

        document["x-owner"] = "platform";

        Assert.Equal("platform", document["x-owner"]!.GetValue<string>());
    }

    [Fact]
    public void ToJsonSchema_Refuses_A_Null_Schema() =>
        Assert.Throws<ArgumentNullException>(() =>
            JsonSchemaExtensions.ToJsonSchema<string, string>(null!));

    private static Schema<Comment> BuildCommentSchema() =>
        Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(static () => CommentSchema)));

    private static Schema<Comment> FreshEachTime() =>
        Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(FreshEachTime)));

    private sealed class AlwaysValid : Schema<string>
    {
        public override bool TryParse(ref ParseContext context, string input, out string output)
        {
            output = input;
            return true;
        }
    }
}
