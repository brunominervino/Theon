using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// The options that decide what a document says, and what it says it had to leave out.
/// </summary>
public class DocumentOptionsTests
{
    private static readonly Schema<Comment> CommentSchema = BuildCommentSchema();

    [Fact]
    public void An_Id_Goes_On_The_Document()
    {
        var document = Theo.String().ToJsonSchema(new JsonSchemaOptions
        {
            Id = "https://example.com/schemas/name.json",
        });

        Assert.Equal("https://example.com/schemas/name.json", document["$id"]!.GetValue<string>());
    }

    // A document that is documentation is better incomplete than absent, so silence is the default.
    [Fact]
    public void A_Rule_With_No_Keyword_Is_Left_Out_Silently_By_Default()
    {
        var schema = Theo.String().NotEmpty().Refine(static v => v.StartsWith('a'), "Must start with a.");

        var document = schema.ToJsonSchema();

        Assert.Equal(1, document["minLength"]!.GetValue<int>());
    }

    // A document that is the contract is worse than useless if it quietly omits a rule, because the
    // client generated from it becomes the only thing checking and it will not check that.
    [Fact]
    public void Asking_To_Be_Told_Names_The_Rule_And_Where_It_Was()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().NotEmpty())
            .Field(x => x.Email, Theo.String().Email().Refine(static v => v.EndsWith(".com", StringComparison.Ordinal), "Must be .com."));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            schema.ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        Assert.Contains("Email: RefineCheck", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Annotate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Schema_With_Nothing_Missing_Does_Not_Throw()
    {
        var schema = Theo.Object<Address>()
            .Field(x => x.Street, Theo.String().MinLength(3))
            .Field(x => x.ZipCode, Theo.String().Length(8));

        var document = schema.ToJsonSchema(new JsonSchemaOptions
        {
            OnUnrepresentable = UnrepresentablePolicy.Throw,
        });

        Assert.NotNull(document["properties"]);
    }

    // A normalization rewrites rather than constrains, so it has no keyword either -- and saying so is
    // the point of asking.
    [Theory]
    [InlineData("OverwriteCheck")]
    public void A_Normalization_Is_Reported_Too(string expected)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Theo.String().Trim().MinLength(3).ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    // The rules after a transform constrain the value this program made, which the dialect cannot
    // describe at all.
    [Fact]
    public void Rules_After_A_Transform_Are_Reported()
    {
        var schema = Theo.String().TryTransform<int>(
            int.TryParse,
            "Must be a whole number.",
            Theo.Int().Min(1).Max(100));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            schema.ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        Assert.Contains("TryTransform", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Temporal_Bound_Is_Reported_As_Well()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Theo.DateOnly().Min(new DateOnly(2026, 1, 1)).ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
            }));

        // Reported by what was lost rather than by which rule lost it, because the rule recorded a
        // bound in good faith and it is the writer that has nowhere to put it.
        Assert.Contains(
            "a minimum bound, which has no keyword for a string",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_Reference_Prefix_Is_The_Callers_To_Choose()
    {
        var described = CommentSchema.ToJsonSchemaDocument(new JsonSchemaOptions
        {
            IncludeDialect = false,
            ReferencePrefix = "#/components/schemas/",
        });

        Assert.Equal("#/components/schemas/Comment", described.Root["$ref"]!.GetValue<string>());

        var definition = Assert.Contains("Comment", described.Definitions);
        Assert.Equal(
            "#/components/schemas/Comment",
            definition["properties"]!["Replies"]!["items"]!["$ref"]!.GetValue<string>());
    }

    // The definitions come back in hand rather than inside the document, which is what an OpenAPI
    // description needs: they belong in components, shared with every other operation.
    [Fact]
    public void The_Definitions_Are_Kept_Out_Of_The_Document()
    {
        var described = CommentSchema.ToJsonSchemaDocument();

        Assert.Null(described.Root["$defs"]);
        Assert.Single(described.Definitions);
    }

    [Fact]
    public void A_Schema_That_Repeats_Nothing_Has_No_Definitions()
    {
        var described = Theo.String().MinLength(3).ToJsonSchemaDocument();

        Assert.Empty(described.Definitions);
        Assert.Equal(3, described.Root["minLength"]!.GetValue<int>());
    }

    [Fact]
    public void ToJsonSchemaDocument_Refuses_A_Null_Schema() =>
        Assert.Throws<ArgumentNullException>(() =>
            JsonSchemaExtensions.ToJsonSchemaDocument<string, string>(null!));

    [Fact]
    public void Errors_Render_As_Lines_A_Person_Can_Read()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Email, Theo.String().Email());

        var result = schema.SafeParse(new CreateUserRequest { Name = "A", Email = "nope" });

        Assert.Equal(
            "Name: Must be at least 3 character(s) long." + Environment.NewLine +
            "Email: Invalid e-mail address.",
            result.Errors.ToPrettyString());
    }

    // An empty path renders as nothing, and a line starting with a colon reads as a mistake.
    [Fact]
    public void An_Error_At_The_Root_Is_Written_With_A_Dash()
    {
        var result = Theo.String().MinLength(3).SafeParse("ab");

        Assert.Equal("-: Must be at least 3 character(s) long.", result.Errors.ToPrettyString());
    }

    [Fact]
    public void No_Errors_Renders_As_Nothing()
    {
        var result = Theo.String().SafeParse("fine");

        Assert.Equal(string.Empty, result.Errors.ToPrettyString());
    }

    [Fact]
    public void ToPrettyString_Refuses_A_Null_List() =>
        Assert.Throws<ArgumentNullException>(() =>
            ValidationErrorFormatting.ToPrettyString(null!));

    private static Schema<Comment> BuildCommentSchema() =>
        Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(static () => CommentSchema)));
}
