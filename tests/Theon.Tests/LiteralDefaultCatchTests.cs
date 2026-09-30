using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// One exact value, a value in place of null, and a failure swallowed by a fallback.
/// </summary>
public class LiteralDefaultCatchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Literal_Accepts_Only_The_Value_It_Was_Given()
    {
        var schema = Theo.Literal("pix");

        Assert.True(schema.IsValid("pix"));
        Assert.False(schema.IsValid("card"));
        Assert.False(schema.IsValid(""));
    }

    [Fact]
    public void Literal_Works_For_A_Value_Type()
    {
        var schema = Theo.Literal(2);

        Assert.True(schema.IsValid(2));
        Assert.False(schema.IsValid(3));
    }

    // Ordinal and case-sensitive, with no comparer parameter. Normalizing first is the idiom, and it
    // says in two rules what a hidden comparer would only imply.
    [Fact]
    public void Literal_Is_Case_Sensitive_And_Normalization_Composes()
    {
        Assert.False(Theo.Literal("pix").IsValid("PIX"));

        var normalized = Theo.String().ToLowerInvariant().Parse("PIX");
        Assert.True(Theo.Literal("pix").IsValid(normalized));
    }

    [Fact]
    public void Literal_Reports_Which_Value_Was_Wanted()
    {
        var result = Theo.Literal("pix").SafeParse("card");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.NotEqual, error.Code);
        Assert.Equal("pix", error.Info.Expected);
        Assert.Equal("Must be pix.", error.Message);
    }

    [Fact]
    public void Literal_Takes_A_Message_Of_Its_Own()
    {
        var result = Theo.Literal(2, "This endpoint only speaks version 2.").SafeParse(1);

        Assert.Equal("This endpoint only speaks version 2.", Assert.Single(result.Errors).Message);
    }

    // A null where an exact value is required is reported as the wrong value rather than the wrong
    // type. The rule is about equality, and "Must be pix." is the sentence a person needs either way.
    [Fact]
    public void Literal_Reports_Null_As_The_Wrong_Value()
    {
        var result = Theo.Literal("pix").SafeParse(null!);

        Assert.Equal(ValidationErrorCode.NotEqual, Assert.Single(result.Errors).Code);
    }

    // The literal's description is formatted with the invariant culture, at construction, so a
    // decimal reads the same wherever the process runs.
    [Fact]
    public void Literal_Describes_A_Decimal_Invariantly()
    {
        var result = Theo.Literal(1.5m).SafeParse(2m);

        Assert.Equal("Must be 1.5.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Literal_Refuses_To_Be_Built_From_Null()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Theo.Literal<string>(null!));

        Assert.Contains("AllowNull", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Answers_For_A_Missing_Value_Type()
    {
        Schema<int?, int> schema = Theo.Int().Min(1).Default(20);

        Assert.Equal(20, schema.Parse(null));
        Assert.Equal(5, schema.Parse(5));
    }

    [Fact]
    public void Default_Still_Validates_A_Value_That_Is_Present()
    {
        var result = Theo.Int().Min(1).Default(20).SafeParse(0);

        Assert.False(result.IsSuccess);
        Assert.Equal(ValidationErrorCode.TooSmall, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Default_Answers_For_A_Missing_Reference_Value()
    {
        Schema<string?, string> schema = Theo.String().NotEmpty().Default("anonymous");

        Assert.Equal("anonymous", schema.Parse(null));
        Assert.Equal("bruno", schema.Parse("bruno"));
    }

    // The edge case that settled the design. The default is not run through the schema: there is
    // nothing to learn from checking a value the schema's own author wrote, and reporting a failure
    // against input the caller never supplied would be a confusing place to find out.
    [Fact]
    public void Default_Is_Not_Itself_Validated()
    {
        var result = Theo.Int().Min(10).Default(1).SafeParse(null);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void Default_Refuses_A_Null_Fallback() =>
        Assert.Throws<ArgumentNullException>(() => Theo.String().Default(null!));

    // Default is the mirror of Required: same two types, opposite answer when the value is absent.
    [Fact]
    public void Default_And_Required_Are_The_Two_Answers_To_The_Same_Question()
    {
        Assert.True(Theo.Int().Default(20).SafeParse(null).IsSuccess);
        Assert.False(Theo.Int().Required().SafeParse(null).IsSuccess);
    }

    [Fact]
    public async Task Default_Answers_On_The_Asynchronous_Path()
    {
        var schema = Theo.String().NotEmpty().Default("anonymous");

        Assert.Equal("anonymous", await schema.ParseAsync(null, cancellationToken: Ct));
        Assert.Equal("bruno", await schema.ParseAsync("bruno", cancellationToken: Ct));
    }

    // An object schema validates an instance rather than rebuilding one, and never writes to it, so
    // a default in a field position is computed and then dropped. Pinned here rather than left to be
    // discovered: the field still passes, and the instance is untouched.
    [Fact]
    public void Default_In_A_Field_Silences_The_Error_And_Changes_Nothing_On_The_Instance()
    {
        var value = new Ticket { Title = "t", CompletedAt = null };

        var schema = Theo.Object<Ticket>()
            .Field(x => x.CompletedAt, Theo.DateTime().Default(new DateTime(2026, 1, 1)));

        Assert.True(schema.IsValid(value));
        Assert.Null(value.CompletedAt);
    }

    [Fact]
    public void Catch_Answers_With_The_Fallback_When_The_Value_Is_Rejected()
    {
        var schema = Theo.Int().Min(1).Max(100).Catch(20);

        Assert.Equal(20, schema.Parse(0));
        Assert.Equal(20, schema.Parse(1000));
        Assert.Equal(50, schema.Parse(50));
    }

    [Fact]
    public void Catch_Reports_Nothing_At_All()
    {
        var result = Theo.Int().Min(1).Catch(20).SafeParse(0);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    // The errors are dropped, not merged and later filtered. A caught field leaves the other
    // fields' complaints exactly as they were.
    [Fact]
    public void Catch_Discards_Only_Its_Own_Subtree()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Age, Theo.Int().Min(18).Catch(18));

        var result = schema.SafeParse(new CreateUserRequest { Name = "ab", Age = 3 });

        Assert.Equal("Name", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Catch_Composes_After_A_Transform()
    {
        var schema = Theo.String().Trim().Transform(int.Parse).Catch(-1);

        Assert.Equal(42, schema.Parse(" 42 "));
    }

    // A rejected value is swallowed; a defect is not. A predicate that throws is a bug in the
    // program, and a fallback that hid it would turn a loud failure into a wrong answer for ever.
    [Fact]
    public void Catch_Does_Not_Swallow_An_Exception()
    {
        var schema = Theo.String()
            .Refine(static _ => throw new InvalidOperationException("boom"), "unused")
            .Catch("fallback");

        Assert.Throws<InvalidOperationException>(() => schema.Parse("anything"));
    }

    [Fact]
    public void Catch_Does_Not_Hide_An_Asynchronous_Rule_Used_Synchronously()
    {
        var schema = Theo.String()
            .RefineAsync(static (_, _) => new ValueTask<bool>(true), "unused")
            .Catch("fallback");

        Assert.Throws<SchemaAsyncUsageException>(() => schema.Parse("anything"));
    }

    [Fact]
    public async Task Catch_Answers_With_The_Fallback_On_The_Asynchronous_Path()
    {
        var schema = Theo.Int().Min(1).Catch(20);

        Assert.Equal(20, await schema.ParseAsync(0, cancellationToken: Ct));
        Assert.Equal(50, await schema.ParseAsync(50, cancellationToken: Ct));
    }

    [Fact]
    public async Task Catch_Swallows_A_Failure_An_Asynchronous_Rule_Raised()
    {
        var schema = Theo.String()
            .RefineAsync(static (_, _) => new ValueTask<bool>(false), "Never available.")
            .Catch("fallback");

        Assert.Equal("fallback", await schema.ParseAsync("anything", cancellationToken: Ct));
    }

    // A schema that runs a child on a private context has to carry the depth across, or a recursive
    // schema inside it never reaches MaxDepth and descends until the stack runs out. With the depth
    // carried, the innermost Lazy reports and the choice says so.
    private static readonly Schema<Node> DeepChoice = BuildDeepChoice();

    private static Schema<Node> BuildDeepChoice() =>
        Theo.OneOf("Not a node we can read.", NodeBranch(), NodeBranch());

    private static Schema<Node> NodeBranch() =>
        Theo.Object<Node>()
            .Field(x => x.Value, Theo.String().NotEmpty())
            .Field(x => x.Child, Theo.Lazy(static () => DeepChoice).AllowNull());

    [Fact]
    public void A_Private_Context_Carries_The_Depth_So_MaxDepth_Stays_Reachable()
    {
        var node = new Node { Value = "leaf" };
        for (var i = 0; i < 20; i++)
        {
            node = new Node { Value = "level", Child = node };
        }

        var result = DeepChoice.SafeParse(node, new ParseOptions { MaxDepth = 3 });

        Assert.False(result.IsSuccess);
        Assert.Equal("Not a node we can read.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_Value_Within_The_Depth_Limit_Still_Passes()
    {
        var node = new Node { Value = "root", Child = new Node { Value = "leaf" } };

        Assert.True(DeepChoice.SafeParse(node, new ParseOptions { MaxDepth = 10 }).IsSuccess);
    }
}
