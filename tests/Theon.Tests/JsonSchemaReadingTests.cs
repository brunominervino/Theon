using System.Text.Json.Nodes;
using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// The other direction: a document somebody else published, turned into a schema.
/// </summary>
/// <remarks>
/// <para>
/// The case this serves is consuming a third party's description, not replacing
/// <c>Theo.Object&lt;T&gt;()</c>. What comes back is a schema over a <see cref="JsonNode"/>, because a
/// document describes JSON and turning that into a type would mean matching property names by
/// reflection.
/// </para>
/// <para>
/// The round-trip tests at the end are the real proof. A document generated from a schema, read back,
/// and generated again has to be the same document — and that is checkable for every schema the
/// library already has.
/// </para>
/// </remarks>
public class JsonSchemaReadingTests
{
    private static Schema<JsonNode?> Read(string document) =>
        Theo.JsonSchema(JsonNode.Parse(document)!);

    private static JsonNode? Value(string json) => JsonNode.Parse(json);

    [Fact]
    public void A_Type_Is_Checked()
    {
        var schema = Read("""{"type":"string"}""");

        Assert.True(schema.SafeParse(Value("\"ok\"")).IsSuccess);

        var result = schema.SafeParse(Value("7"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("string", error.Info.Expected);
        Assert.Equal("integer", error.Info.Received);
    }

    // "integer" in this dialect is about the value and not the encoding, so 1.0 is one and 1.5 is not.
    [Theory]
    [InlineData("1", true)]
    [InlineData("1.0", true)]
    [InlineData("1.5", false)]
    public void An_Integer_Is_A_Number_With_Nothing_After_The_Point(string json, bool accepted) =>
        Assert.Equal(accepted, Read("""{"type":"integer"}""").SafeParse(Value(json)).IsSuccess);

    [Fact]
    public void Every_Integer_Is_A_Number_And_Not_The_Other_Way_Round()
    {
        Assert.True(Read("""{"type":"number"}""").SafeParse(Value("7")).IsSuccess);
        Assert.False(Read("""{"type":"integer"}""").SafeParse(Value("7.5")).IsSuccess);
    }

    [Fact]
    public void A_Type_Union_With_Null_Accepts_Null()
    {
        var schema = Read("""{"type":["string","null"]}""");

        Assert.True(schema.SafeParse(Value("null")).IsSuccess);
        Assert.True(schema.SafeParse(Value("\"ok\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("7")).IsSuccess);
    }

    [Fact]
    public void A_Named_Type_Without_Null_Refuses_Null()
    {
        var result = Read("""{"type":"string"}""").SafeParse(Value("null"));

        Assert.Equal("A value is required.", Assert.Single(result.Errors).Message);
    }

    // A document that names no type constrains nothing about what kind of value arrives, which
    // includes null.
    [Fact]
    public void A_Document_That_Names_No_Type_Accepts_Anything()
    {
        var schema = Read("{}");

        Assert.True(schema.SafeParse(Value("null")).IsSuccess);
        Assert.True(schema.SafeParse(Value("7")).IsSuccess);
        Assert.True(schema.SafeParse(Value("""{"a":1}""")).IsSuccess);
    }

    [Fact]
    public void String_Bounds_And_Patterns_Are_Checked()
    {
        var schema = Read("""{"type":"string","minLength":2,"maxLength":4,"pattern":"\\A[a-z]+\\z"}""");

        Assert.True(schema.SafeParse(Value("\"abc\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"a\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"abcde\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"AB\"")).IsSuccess);
    }

    // The formats are the library's own rules, not a second implementation. A reading side with its
    // own e-mail rule would drift from the writing side and stop accepting its own documents.
    [Fact]
    public void A_Known_Format_Is_Checked_By_The_Rule_This_Library_Already_Has()
    {
        var schema = Read("""{"type":"string","format":"email"}""");

        Assert.True(schema.SafeParse(Value("\"ada@example.com\"")).IsSuccess);

        var result = schema.SafeParse(Value("\"nope\""));
        Assert.Equal("Invalid e-mail address.", Assert.Single(result.Errors).Message);
    }

    // The one place something is let through, and it is what the dialect says: format is an annotation
    // unless the format-assertion vocabulary is in use. Refusing would also make this useless on real
    // documents, which are full of names nobody has written a rule for.
    [Fact]
    public void An_Unknown_Format_Is_Ignored() =>
        Assert.True(Read("""{"type":"string","format":"password"}""")
            .SafeParse(Value("\"anything\"")).IsSuccess);

    [Fact]
    public void Numeric_Bounds_Are_Checked_Inclusively_And_Exclusively()
    {
        var inclusive = Read("""{"type":"integer","minimum":1,"maximum":10}""");

        Assert.True(inclusive.SafeParse(Value("1")).IsSuccess);
        Assert.True(inclusive.SafeParse(Value("10")).IsSuccess);
        Assert.False(inclusive.SafeParse(Value("0")).IsSuccess);

        var exclusive = Read("""{"type":"integer","exclusiveMinimum":1,"exclusiveMaximum":10}""");

        Assert.False(exclusive.SafeParse(Value("1")).IsSuccess);
        Assert.False(exclusive.SafeParse(Value("10")).IsSuccess);
        Assert.True(exclusive.SafeParse(Value("5")).IsSuccess);
    }

    // Both keywords of a pair are legal together and are independent assertions. Reading one and
    // dropping the other would make the schema accept what the document rejects: {"minimum":1,
    // "exclusiveMinimum":5} would become "greater than 1" and let 2 through. Refused, like every other
    // thing this reader cannot honour.
    [Theory]
    [InlineData("minimum", "exclusiveMinimum")]
    [InlineData("maximum", "exclusiveMaximum")]
    [InlineData("minItems", "minProperties")]
    [InlineData("maxItems", "maxProperties")]
    public void Both_Keywords_Of_A_Pair_Are_Refused_Rather_Than_Halved(string keyword, string other)
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Read("{\"" + keyword + "\":1,\"" + other + "\":5}"));

        Assert.Contains(keyword, exception.Message, StringComparison.Ordinal);
        Assert.Contains(other, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Divisor_Is_Checked()
    {
        var schema = Read("""{"type":"integer","multipleOf":5}""");

        Assert.True(schema.SafeParse(Value("15")).IsSuccess);
        Assert.Equal(
            ValidationErrorCode.NotMultipleOf,
            Assert.Single(schema.SafeParse(Value("7")).Errors).Code);
    }

    [Fact]
    public void Properties_Are_Checked_At_Their_Own_Path()
    {
        var schema = Read(
            """{"type":"object","properties":{"name":{"type":"string","minLength":2}}}""");

        var result = schema.SafeParse(Value("""{"name":"a"}"""));

        Assert.Equal("name", Assert.Single(result.Errors).Path.ToString());
    }

    // A missing key is a real thing here, unlike everywhere else in this library: this is the one
    // schema whose input has not been through a type first.
    [Fact]
    public void A_Required_Property_That_Is_Absent_Is_Reported()
    {
        var schema = Read(
            """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""");

        Assert.True(schema.SafeParse(Value("""{"name":"ok"}""")).IsSuccess);

        var result = schema.SafeParse(Value("{}"));
        var error = Assert.Single(result.Errors);
        Assert.Equal("name", error.Path.ToString());
        Assert.Equal("A value is required.", error.Message);
    }

    // "required" is independent of "properties": a name may be demanded without anything being said
    // about what its value looks like. Reading only the names that "properties" also mentions would
    // drop the demand, and accepting {} for a document that requires "id" is the exact direction this
    // reader refuses everywhere else.
    [Fact]
    public void A_Required_Name_With_No_Schema_Of_Its_Own_Is_Still_Demanded()
    {
        var schema = Read("""{"type":"object","required":["id"]}""");

        Assert.True(schema.SafeParse(Value("""{"id":1}""")).IsSuccess);

        var result = schema.SafeParse(Value("{}"));
        Assert.Equal("id", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void A_Required_Name_Beside_Named_Properties_Is_Demanded_Too()
    {
        var schema = Read(
            """{"type":"object","properties":{"name":{"type":"string"}},"required":["name","id"]}""");

        Assert.True(schema.SafeParse(Value("""{"name":"x","id":1}""")).IsSuccess);

        var result = schema.SafeParse(Value("""{"name":"x"}"""));
        Assert.Equal("id", Assert.Single(result.Errors).Path.ToString());
    }

    // A type set of nothing but null restricts the value to null. Reading it as "no type was named"
    // would accept every value, which is the opposite of what it says.
    [Theory]
    [InlineData("""{"type":"null"}""")]
    [InlineData("""{"type":["null"]}""")]
    public void A_Type_Of_Only_Null_Accepts_Only_Null(string document)
    {
        var schema = Read(document);

        Assert.True(schema.SafeParse(Value("null")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"hello\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("7")).IsSuccess);
    }

    [Fact]
    public void A_Type_Of_Only_Null_Is_Written_Back_As_Itself()
    {
        var again = Theo.JsonSchema(JsonNode.Parse("""{"type":"null"}""")!).ToJsonSchema();

        Assert.Equal("null", again["type"]!.GetValue<string>());
    }

    // A reference that comes back to itself through anyOf advances nothing about the value, so the
    // guard on how deeply a value is nested never fires. Legal, written by nobody here, and a stack
    // overflow -- which cannot be caught -- from a document somebody else published.
    [Fact]
    public void A_Reference_That_Loops_Through_AnyOf_Is_Reported_Rather_Than_Ending_The_Stack()
    {
        var schema = Read(
            """{"$ref":"#/$defs/A","$defs":{"A":{"anyOf":[{"$ref":"#/$defs/A"}]}}}""");

        Assert.False(schema.SafeParse(Value("1")).IsSuccess);
    }

    [Fact]
    public void A_Reference_That_Loops_Through_AllOf_Is_Reported_Too()
    {
        var schema = Read(
            """{"$ref":"#/$defs/A","$defs":{"A":{"allOf":[{"$ref":"#/$defs/A"}]}}}""");

        Assert.False(schema.SafeParse(Value("1")).IsSuccess);
    }

    [Fact]
    public void A_Property_That_Is_Not_Required_May_Be_Absent() =>
        Assert.True(Read("""{"type":"object","properties":{"name":{"type":"string"}}}""")
            .SafeParse(Value("{}")).IsSuccess);

    [Fact]
    public void Additional_Properties_Govern_The_Keys_Properties_Did_Not_Name()
    {
        var schema = Read(
            """
            {"type":"object","properties":{"id":{"type":"integer"}},
             "additionalProperties":{"type":"string"}}
            """);

        Assert.True(schema.SafeParse(Value("""{"id":1,"tag":"x"}""")).IsSuccess);

        var result = schema.SafeParse(Value("""{"id":1,"tag":7}"""));
        Assert.Equal("tag", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Array_Items_And_Counts_Are_Checked()
    {
        var schema = Read(
            """{"type":"array","items":{"type":"integer"},"minItems":1,"maxItems":3}""");

        Assert.True(schema.SafeParse(Value("[1,2]")).IsSuccess);
        Assert.False(schema.SafeParse(Value("[]")).IsSuccess);
        Assert.False(schema.SafeParse(Value("[1,2,3,4]")).IsSuccess);

        var result = schema.SafeParse(Value("""[1,"x"]"""));
        Assert.Equal("[1]", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Unique_Items_Is_Reported_At_The_Repeat()
    {
        var schema = Read("""{"type":"array","items":{"type":"integer"},"uniqueItems":true}""");

        Assert.True(schema.SafeParse(Value("[1,2,3]")).IsSuccess);

        var result = schema.SafeParse(Value("[1,2,1]"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.Duplicate, error.Code);
        Assert.Equal("[2]", error.Path.ToString());
    }

    [Fact]
    public void Enum_And_Const_Are_Checked()
    {
        Assert.True(Read("""{"enum":["a","b"]}""").SafeParse(Value("\"a\"")).IsSuccess);
        Assert.False(Read("""{"enum":["a","b"]}""").SafeParse(Value("\"c\"")).IsSuccess);

        Assert.True(Read("""{"const":"card"}""").SafeParse(Value("\"card\"")).IsSuccess);
        Assert.Equal(
            ValidationErrorCode.NotEqual,
            Assert.Single(Read("""{"const":"card"}""").SafeParse(Value("\"cash\"")).Errors).Code);
    }

    [Fact]
    public void AnyOf_Accepts_A_Value_Satisfying_One_Branch()
    {
        var schema = Read("""{"anyOf":[{"type":"string"},{"type":"integer"}]}""");

        Assert.True(schema.SafeParse(Value("\"x\"")).IsSuccess);
        Assert.True(schema.SafeParse(Value("7")).IsSuccess);
        Assert.False(schema.SafeParse(Value("true")).IsSuccess);
    }

    [Fact]
    public void AllOf_Requires_Every_Branch()
    {
        var schema = Read(
            """{"allOf":[{"type":"string","minLength":2},{"type":"string","maxLength":4}]}""");

        Assert.True(schema.SafeParse(Value("\"abc\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"a\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"abcde\"")).IsSuccess);
    }

    [Fact]
    public void A_Reference_Is_Followed()
    {
        var schema = Read(
            """
            {"type":"object","properties":{"home":{"$ref":"#/$defs/Address"}},
             "$defs":{"Address":{"type":"object","properties":{"zip":{"type":"string","minLength":5}}}}}
            """);

        Assert.True(schema.SafeParse(Value("""{"home":{"zip":"12345"}}""")).IsSuccess);

        var result = schema.SafeParse(Value("""{"home":{"zip":"1"}}"""));
        Assert.Equal("home.zip", Assert.Single(result.Errors).Path.ToString());
    }

    // The OpenAPI spelling, which is the one that matters for the case this feature exists for. It is
    // not a special case: the pointer is walked from the document root like any other.
    [Fact]
    public void A_Reference_Into_Components_Is_Followed_Too()
    {
        var schema = Read(
            """
            {"$ref":"#/components/schemas/Name",
             "components":{"schemas":{"Name":{"type":"string","minLength":2}}}}
            """);

        Assert.True(schema.SafeParse(Value("\"ok\"")).IsSuccess);
        Assert.False(schema.SafeParse(Value("\"x\"")).IsSuccess);
    }

    [Fact]
    public void A_Schema_That_Contains_Itself_Is_Read_And_Checked()
    {
        var schema = Read(
            """
            {"$ref":"#/$defs/Comment",
             "$defs":{"Comment":{"type":"object",
               "properties":{"body":{"type":"string","minLength":1},
                             "replies":{"type":"array","items":{"$ref":"#/$defs/Comment"}}},
               "required":["body"]}}}
            """);

        Assert.True(schema.SafeParse(Value(
            """{"body":"hello","replies":[{"body":"hi","replies":[]}]}""")).IsSuccess);

        var result = schema.SafeParse(Value("""{"body":"hello","replies":[{"replies":[]}]}"""));

        Assert.Equal("replies[0].body", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void A_Value_Nested_Beyond_The_Limit_Is_Reported_Rather_Than_Ending_The_Stack()
    {
        var schema = Read(
            """
            {"$ref":"#/$defs/Node",
             "$defs":{"Node":{"type":"object","properties":{"next":{"$ref":"#/$defs/Node"}}}}}
            """);

        var deep = new JsonObject();
        var current = deep;

        for (var i = 0; i < 200; i++)
        {
            var next = new JsonObject();
            current["next"] = next;
            current = next;
        }

        Assert.False(schema.SafeParse(deep).IsSuccess);
    }

    // Refusing is the conservative direction. Ignoring an assertion would make the schema accept
    // values the document rejects, while the caller believes it was checked.
    [Theory]
    [InlineData("oneOf")]
    [InlineData("not")]
    [InlineData("if")]
    [InlineData("patternProperties")]
    [InlineData("prefixItems")]
    [InlineData("contains")]
    [InlineData("unevaluatedProperties")]
    public void A_Keyword_That_Cannot_Be_Honoured_Is_Refused_When_The_Schema_Is_Built(string keyword)
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Read("{\"type\":\"object\",\"" + keyword + "\":{}}"));

        Assert.Contains(keyword, exception.Message, StringComparison.Ordinal);
    }

    // A keyword that asserts nothing is ignored, which is what the dialect requires of an unknown one.
    [Fact]
    public void A_Keyword_That_Asserts_Nothing_Is_Ignored()
    {
        var schema = Read(
            """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"https://example.com/x",
             "title":"A name","description":"...","deprecated":true,"default":"x",
             "examples":["x"],"x-vendor":{"anything":true},"type":"string"}
            """);

        Assert.True(schema.SafeParse(Value("\"ok\"")).IsSuccess);
    }

    [Fact]
    public void A_Reference_Outside_The_Document_Is_Refused()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Read("""{"$ref":"https://example.com/other.json#/$defs/Thing"}"""));

        Assert.Contains("outside this document", exception.Message, StringComparison.Ordinal);
    }

    // A pattern out of somebody else's document is text from a stranger. Linear-time matching by
    // construction is how this library answers that everywhere else, and a pattern the engine will not
    // build that way is refused rather than matched by a backtracking one.
    [Fact]
    public void A_Pattern_That_Cannot_Match_In_Linear_Time_Is_Refused()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            Read("""{"type":"string","pattern":"\\A(?=a)ab\\z"}"""));

        Assert.Contains("linear time", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Null_Document_Is_Refused() =>
        Assert.Throws<ArgumentNullException>(() => Theo.JsonSchema(null!));

    public static TheoryData<string, Schema<string>> StringSchemas() => new()
    {
        { "plain", Theo.String() },
        { "bounded", Theo.String().MinLength(2).MaxLength(40) },
        { "email", Theo.String().Email() },
        { "uuid", Theo.String().Uuid() },
        { "hostname", Theo.String().Hostname() },
        { "iban", Theo.String().Iban() },
    };

    // The cycle closed. A document generated from a schema, read back into a schema, and generated
    // again has to be the same document -- which is the strongest check available on both directions
    // at once, and it is writable for every schema the library already has.
    [Theory]
    [MemberData(nameof(StringSchemas))]
    public void A_String_Schema_Survives_The_Round_Trip(string name, Schema<string> schema) =>
        AssertRoundTrips(name, schema.ToJsonSchema());

    [Fact]
    public void Every_Shape_This_Library_Writes_Survives_The_Round_Trip()
    {
        var documents = new (string Name, JsonObject Document)[]
        {
            ("int", Theo.Int().Min(1).Max(100).ToJsonSchema()),
            ("decimal", Theo.Decimal().MultipleOf(0.5m).ToJsonSchema()),
            ("nullable int", Theo.Int().Min(1).AllowNull().ToJsonSchema()),
            ("enum", Theo.Enum<TaskStatus>().ToJsonSchema()),
            ("literal", Theo.Literal("card").ToJsonSchema()),
            ("collection", Theo.Collection(Theo.String().Email()).MinCount(1).MaxCount(5).ToJsonSchema()),
            ("set", Theo.Set(Theo.String()).MaxCount(3).ToJsonSchema()),
            ("record", Theo.Record(Theo.Int().Min(0)).ToJsonSchema()),
            ("object", Theo.Object<CreateUserRequest>()
                .Field(x => x.Name, Theo.String().MinLength(2))
                .Field(x => x.Email, Theo.String().Email())
                .Field(x => x.Age, Theo.Int().Min(18).Max(120))
                .ToJsonSchema()),
            ("one of", Theo.OneOf("Pick one.", Theo.Literal("card"), Theo.Literal("cash"))
                .ToJsonSchema()),
            ("and", Theo.String().MinLength(2).And(Theo.String().MaxLength(8)).ToJsonSchema()),
        };

        foreach (var (name, document) in documents)
        {
            AssertRoundTrips(name, document);
        }
    }

    // A recursive document comes back structurally the same, with the definition named after the CLR
    // type the schema produces rather than after whatever the original called it. That renaming is the
    // one thing the cycle does not preserve, and it is here so that nobody discovers it by accident.
    [Fact]
    public void A_Recursive_Document_Comes_Back_Recursive_Under_A_Name_Of_Our_Own()
    {
        var original = JsonNode.Parse(
            """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","$ref":"#/$defs/Comment",
             "$defs":{"Comment":{"type":"object",
               "properties":{"body":{"type":"string"},
                             "replies":{"type":"array","items":{"$ref":"#/$defs/Comment"}}}}}}
            """)!;

        var again = Theo.JsonSchema(original).ToJsonSchema();

        Assert.Equal("#/$defs/JsonNode", again["$ref"]!.GetValue<string>());

        var definition = again["$defs"]!["JsonNode"]!;
        Assert.Equal("object", definition["type"]!.GetValue<string>());
        Assert.Equal(
            "#/$defs/JsonNode",
            definition["properties"]!["replies"]!["items"]!["$ref"]!.GetValue<string>());
    }

    private static void AssertRoundTrips(string name, JsonObject document)
    {
        var again = Theo.JsonSchema(document).ToJsonSchema();

        Assert.True(
            JsonNode.DeepEquals(document, again),
            $"The {name} document did not survive the round trip." + Environment.NewLine +
            "out:  " + document.ToJsonString() + Environment.NewLine +
            "back: " + again.ToJsonString());
    }
}
