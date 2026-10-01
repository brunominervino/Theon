using System.Text.Json.Nodes;

namespace Theon.Tests;

/// <summary>
/// The escape hatch for a document that has to say something this library will not invent.
/// </summary>
/// <remarks>
/// <c>UnrepresentablePolicy.Throw</c> says what a document had to leave out and gives no way to put
/// it back. An amendment is the way back: a function that receives every node once it has been
/// written and may change it, so a caller who knows what their own refinement means can express it
/// themselves.
/// </remarks>
public class DocumentAmendmentTests
{
    private static readonly Schema<Comment> CommentSchema = BuildCommentSchema();

    [Fact]
    public void An_Amendment_Can_Add_A_Keyword()
    {
        var document = Theo.String().MinLength(3).ToJsonSchema(new JsonSchemaOptions
        {
            Amend = static node => node.Json["x-widget"] = "text",
        });

        Assert.Equal("text", document["x-widget"]!.GetValue<string>());
        Assert.Equal(3, document["minLength"]!.GetValue<int>());
    }

    [Fact]
    public void An_Amendment_Can_Remove_A_Keyword()
    {
        var document = Theo.String().MinLength(3).ToJsonSchema(new JsonSchemaOptions
        {
            Amend = static node => node.Json.Remove("minLength"),
        });

        Assert.Null(document["minLength"]);
    }

    // The path is what makes an amendment able to aim. Without it the only thing a caller could do is
    // change every node in the document the same way.
    [Fact]
    public void Every_Node_Is_Offered_With_The_Path_It_Is_At()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String())
            .Field(x => x.Email, Theo.String().Email());

        var seen = new List<string>();

        schema.ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => seen.Add(node.Path),
        });

        Assert.Equal(["Name", "Email", string.Empty], seen);
    }

    [Fact]
    public void A_Collections_Element_Is_Offered_At_The_Collections_Path()
    {
        var seen = new List<string>();

        Theo.Object<Comment>()
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(() => CommentSchema)))
            .ToJsonSchema(new JsonSchemaOptions
            {
                Amend = node => seen.Add(node.Path),
            });

        Assert.Contains("Replies[]", seen);
    }

    [Fact]
    public void A_Maps_Value_Is_Offered_Under_A_Star()
    {
        var seen = new List<string>();

        Theo.Record(Theo.String()).ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => seen.Add(node.Path),
        });

        Assert.Contains("*", seen);
    }

    [Fact]
    public void A_Definition_Is_Offered_Under_Its_Name()
    {
        var seen = new List<string>();

        CommentSchema.ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => seen.Add(node.Path),
        });

        Assert.Contains("$defs/Comment", seen);
    }

    // The definitions reach the amendment whether they end up inside the document or beside it, and
    // they reach it exactly once either way.
    [Fact]
    public void A_Definition_Kept_Out_Of_The_Document_Is_Still_Offered_Once()
    {
        var seen = new List<string>();

        CommentSchema.ToJsonSchemaDocument(new JsonSchemaOptions
        {
            Amend = node => seen.Add(node.Path),
        });

        Assert.Single(seen, static path => path == "$defs/Comment");
    }

    // Children first, so an amendment on an object sees the fields it contains as they will be read,
    // including whatever an amendment on one of those fields did to it.
    [Fact]
    public void A_Node_Is_Offered_After_Its_Children()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String())
            .Field(x => x.Email, Theo.String());

        JsonNode? nameAsTheObjectSawIt = null;

        var document = schema.ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node =>
            {
                if (node.Path == "Name")
                {
                    node.Json["x-widget"] = "text";
                }
                else if (node.Path.Length == 0)
                {
                    nameAsTheObjectSawIt = node.Json["properties"]!["Name"]!["x-widget"]!.DeepClone();
                }
            },
        });

        Assert.Equal("text", nameAsTheObjectSawIt!.GetValue<string>());
        Assert.Equal("text", document["properties"]!["Name"]!["x-widget"]!.GetValue<string>());
    }

    [Fact]
    public void A_Node_Is_Told_Which_Of_Its_Rules_Could_Not_Be_Expressed()
    {
        var schema = Theo.String().Refine(static value => value.StartsWith('a'), "Must start with a.");

        var reported = new List<string>();

        schema.ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => reported.AddRange(node.Unrepresentable),
        });

        Assert.Equal(["RefineCheck"], reported);
    }

    // A bound the rule recorded in good faith and the writer had nowhere to put is reported the same
    // way, because a caller patching the node needs to know about both kinds.
    [Fact]
    public void A_Dropped_Keyword_Is_Reported_To_The_Amendment_Too()
    {
        var reported = new List<string>();

        Theo.DateOnly().Min(new DateOnly(2026, 1, 1)).ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => reported.AddRange(node.Unrepresentable),
        });

        Assert.Contains(
            reported,
            static rule => rule.Contains("minimum bound", StringComparison.Ordinal));
    }

    // The point of the whole thing. An amendment that expresses a rule itself says so, and the policy
    // stops reporting the node it expressed.
    [Fact]
    public void An_Amendment_That_Says_It_Expressed_A_Rule_Stops_The_Policy_Reporting_It()
    {
        var schema = Theo.String().Refine(static value => value.StartsWith('a'), "Must start with a.");

        var document = schema.ToJsonSchema(new JsonSchemaOptions
        {
            OnUnrepresentable = UnrepresentablePolicy.Throw,
            Amend = static node =>
            {
                if (node.Unrepresentable.Count > 0)
                {
                    node.Json["pattern"] = @"\Aa";
                    node.Expressed = true;
                }
            },
        });

        Assert.Equal(@"\Aa", document["pattern"]!.GetValue<string>());
    }

    // Patching without saying so still throws. The policy is about whether the document states every
    // rule, and the writer cannot tell a keyword that expresses a refinement from one that does not.
    [Fact]
    public void An_Amendment_That_Patches_Without_Saying_So_Is_Still_Reported()
    {
        var schema = Theo.String().Refine(static value => value.StartsWith('a'), "Must start with a.");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            schema.ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
                Amend = static node => node.Json["pattern"] = @"\Aa",
            }));

        Assert.Contains("RefineCheck", exception.Message, StringComparison.Ordinal);
    }

    // Expressing one node says nothing about any other. A document with two refinements in it and one
    // amendment that only understands one of them has to still be told about the other.
    [Fact]
    public void Expressing_One_Node_Leaves_The_Others_Reported()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().Refine(static v => v.Length > 1, "Too short."))
            .Field(x => x.Email, Theo.String().Refine(static v => v.Length > 1, "Too short."));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            schema.ToJsonSchema(new JsonSchemaOptions
            {
                OnUnrepresentable = UnrepresentablePolicy.Throw,
                Amend = static node =>
                {
                    if (node.Path == "Name")
                    {
                        node.Expressed = true;
                    }
                },
            }));

        Assert.DoesNotContain("Name: RefineCheck", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Email: RefineCheck", exception.Message, StringComparison.Ordinal);
    }

    // A reference has nothing of its own to lose, so there is nothing to express -- but it is still
    // offered, because a caller may want to put a sibling annotation beside it.
    [Fact]
    public void A_Reference_Node_Is_Offered_With_Nothing_To_Express()
    {
        var reported = new List<string>();

        var document = CommentSchema.ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node =>
            {
                if (node.Json["$ref"] is not null)
                {
                    reported.AddRange(node.Unrepresentable);
                    node.Json["description"] = "A reply.";
                }
            },
        });

        Assert.Empty(reported);
        Assert.Equal(
            "A reply.",
            document["$defs"]!["Comment"]!["properties"]!["Replies"]!["items"]!["description"]!
                .GetValue<string>());
    }

    [Fact]
    public void No_Amendment_Changes_Nothing()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(2))
            .Field(x => x.Email, Theo.String().Email());

        var plain = schema.ToJsonSchema();
        var amended = schema.ToJsonSchema(new JsonSchemaOptions { Amend = static _ => { } });

        Assert.Equal(plain.ToJsonString(), amended.ToJsonString());
    }

    private static Schema<Comment> BuildCommentSchema()
    {
        Schema<Comment>? schema = null;

        schema = Theo.Object<Comment>()
            .Field(x => x.Body, Theo.String().NotEmpty())
            .Field(x => x.Replies, Theo.Collection(Theo.Lazy(() => schema!)));

        return schema;
    }
}
