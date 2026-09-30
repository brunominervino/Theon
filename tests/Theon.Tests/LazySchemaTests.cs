using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// A schema that refers to itself, for values that contain values of their own shape.
/// </summary>
public class LazySchemaTests
{
    private static Schema<Comment> CommentSchema() =>
        Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(CommentSchema)));

    private static Comment Leaf(string body) => new() { Body = body };

    [Fact]
    public void A_Flat_Value_Passes() => Assert.True(CommentSchema().IsValid(Leaf("hello")));

    [Fact]
    public void A_Nested_Value_Is_Validated_All_The_Way_Down()
    {
        var value = new Comment
        {
            Body = "root",
            Replies = [new Comment { Body = "child", Replies = [Leaf("")] }],
        };

        var result = CommentSchema().SafeParse(value);

        Assert.Equal("Replies[0].Replies[0].Body", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Every_Failure_In_The_Tree_Is_Reported()
    {
        var value = new Comment
        {
            Body = "root",
            Replies = [Leaf(""), Leaf("fine"), Leaf("")],
        };

        var result = CommentSchema().SafeParse(value);

        Assert.Equal(
            new[] { "Replies[0].Body", "Replies[2].Body" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void A_Factory_That_Rebuilds_Costs_One_Schema_Per_Level()
    {
        // Each Theo.Lazy call makes its own wrapper, and a factory that constructs from scratch
        // therefore builds a fresh schema for every level of the value. Correct, and wasteful: a
        // thousand-node tree would build a thousand schemas.
        var builds = 0;

        Schema<Comment> Rebuild()
        {
            builds++;
            return Theo.Object<Comment>()
                .Field(x => x.Body, Theo.String().NotEmpty())
                .Field(x => x.Replies, Theo.Collection(Theo.Lazy(Rebuild)));
        }

        var deep = new Comment { Body = "a", Replies = [new Comment { Body = "b", Replies = [Leaf("c")] }] };

        Assert.True(Theo.Lazy(Rebuild).IsValid(deep));
        Assert.Equal(3, builds);
    }

    private static int _sharedBuilds;

    private static readonly Schema<Comment> Shared = BuildShared();

    private static Schema<Comment> BuildShared()
    {
        _sharedBuilds++;
        return Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(() => Shared)));
    }

    [Fact]
    public void A_Factory_Returning_A_Shared_Instance_Builds_Once()
    {
        // The pattern to reach for: the factory hands back the same schema rather than making a
        // new one, so depth costs nothing to build however deep the value goes.
        var before = _sharedBuilds;

        var deep = new Comment
        {
            Body = "a",
            Replies = [new Comment { Body = "b", Replies = [new Comment { Body = "c", Replies = [Leaf("d")] }] }],
        };

        Assert.True(Shared.IsValid(deep));
        Assert.Equal(before, _sharedBuilds);
    }

    [Fact]
    public void A_Value_That_Contains_Itself_Reports_Instead_Of_Overflowing()
    {
        // Without a depth limit this recurses until the stack dies, which cannot be caught.
        var cycle = new Comment { Body = "loop" };
        cycle.Replies = [cycle];

        var result = CommentSchema().SafeParse(cycle);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Code == ValidationErrorCode.Custom);
    }

    [Fact]
    public void The_Depth_Limit_Is_Configurable()
    {
        var value = new Comment
        {
            Body = "1",
            Replies = [new Comment { Body = "2", Replies = [Leaf("3")] }],
        };

        Assert.True(CommentSchema().IsValid(value));
        Assert.False(CommentSchema().SafeParse(value, new ParseOptions { MaxDepth = 2 }).IsSuccess);
    }

    [Fact]
    public async Task Recursion_Works_On_The_Asynchronous_Path_Too()
    {
        var schema = Theo.Lazy(CommentSchema);
        var value = new Comment { Body = "root", Replies = [Leaf("")] };

        var result = await schema.SafeParseAsync(value, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Replies[0].Body", Assert.Single(result.Errors).Path.ToString());
    }
}
