using Theon.Errors;

namespace Theon.Tests;

public class PathTests
{
    [Fact]
    public void A_Root_Path_Renders_As_Empty()
    {
        Assert.True(ValidationPath.Root.IsRoot);
        Assert.Equal(string.Empty, ValidationPath.Root.ToString());
    }

    [Fact]
    public void Deep_Nesting_Renders_With_Dots()
    {
        var schema = Theo.Object<Level1>()
            .Field(x => x.Child, Theo.Object<Level2>()
                .Field(y => y.Child, Theo.Object<Level3>()
                    .Field(z => z.Value, Theo.String().MinLength(5))));

        var result = schema.SafeParse(new Level1());

        Assert.Equal("Child.Child.Value", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Nesting_Deeper_Than_The_Inline_Buffer_Still_Works()
    {
        // The context holds eight path levels inline; go past that so the overflow array is used.
        const int Depth = 12;

        var schema = BuildChain(Depth);
        var value = BuildValue(Depth);

        var result = schema.SafeParse(value);

        var path = Assert.Single(result.Errors).Path;
        Assert.Equal(Depth + 1, path.Length);
        Assert.Equal(
            string.Join('.', Enumerable.Repeat("Child", Depth)) + ".Value",
            path.ToString());
    }

    private static Schema<Node> BuildChain(int depth)
    {
        var schema = Theo.Object<Node>().Field(x => x.Value, Theo.String().MinLength(5));

        for (var i = 0; i < depth; i++)
        {
            schema = Theo.Object<Node>().Field(x => x.Child, schema.AllowNull());
        }

        return schema;
    }

    private static Node BuildValue(int depth)
    {
        var node = new Node { Value = string.Empty };

        for (var i = 0; i < depth; i++)
        {
            node = new Node { Child = node, Value = "long enough" };
        }

        return node;
    }

    [Fact]
    public void Index_Segments_Render_In_Brackets()
    {
        var path = new[] { PathSegment.Property("users"), PathSegment.Index(3), PathSegment.Property("zip") };

        Assert.Equal("users", path[0].ToString());
        Assert.Equal("[3]", path[1].ToString());
        Assert.True(path[1].IsIndex);
        Assert.Equal(3, path[1].ElementIndex);
    }

    [Fact]
    public void Segments_Compare_By_Value()
    {
        Assert.Equal(PathSegment.Property("a"), PathSegment.Property("a"));
        Assert.NotEqual(PathSegment.Property("a"), PathSegment.Property("b"));
        Assert.Equal(PathSegment.Index(1), PathSegment.Index(1));
        Assert.NotEqual(PathSegment.Property("a"), PathSegment.Index(0));
    }
}
