using System.Collections;
using System.Reflection;
using Theon.Metadata;

namespace Theon.Tests;

/// <summary>
/// The description model as a public contract: a custom schema describing itself, and a generator
/// written by somebody else.
/// </summary>
/// <remarks>
/// Both of these were impossible while the model was internal. A schema from another assembly could
/// not override the hook, so a document generated for it said "anything"; and nobody could write a
/// generator for a format this library does not ship.
/// </remarks>
public class PublicDescriptionTests
{
    private static readonly Schema<Comment> CommentSchema = BuildCommentSchema();

    // A custom schema. Nothing here is reachable from outside the assembly that declares it, which is
    // exactly the point: this is what any consumer can now write.
    private sealed class UlidSchema : Schema<string>
    {
        public override bool TryParse(ref ParseContext context, string input, out string output)
        {
            output = input;
            return input?.Length == 26;
        }

        public override SchemaDescription Describe(DescriptionContext context) => new()
        {
            Kind = SchemaKind.String,
            Format = "ulid",
            MinLength = 26,
            MaxLength = 26,
        };
    }

    // A custom schema that says nothing, to pin the other half of the contract: the default is still
    // silence, and silence still means "constrains nothing".
    private sealed class SilentSchema : Schema<string>
    {
        public override bool TryParse(ref ParseContext context, string input, out string output)
        {
            output = input;
            return true;
        }
    }

    private sealed class DirectionReportingSchema(List<DescriptionDirection> seen) : Schema<string>
    {
        public override bool TryParse(ref ParseContext context, string input, out string output)
        {
            output = input;
            return true;
        }

        public override SchemaDescription Describe(DescriptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            seen.Add(context.Direction);
            return new SchemaDescription { Kind = SchemaKind.String };
        }
    }

    [Fact]
    public void A_Custom_Schema_Can_Describe_Itself()
    {
        var document = new UlidSchema().ToJsonSchema();

        Assert.Equal("string", document["type"]!.GetValue<string>());
        Assert.Equal("ulid", document["format"]!.GetValue<string>());
        Assert.Equal(26, document["minLength"]!.GetValue<int>());
        Assert.Equal(26, document["maxLength"]!.GetValue<int>());
    }

    [Fact]
    public void A_Custom_Schema_Composes_Into_An_Object_And_Is_Described_There()
    {
        var document = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, new UlidSchema())
            .ToJsonSchema();

        Assert.Equal("ulid", document["properties"]!["Name"]!["format"]!.GetValue<string>());
    }

    [Fact]
    public void A_Schema_That_Says_Nothing_Still_Constrains_Nothing()
    {
        var document = new SilentSchema().ToJsonSchema();

        Assert.Null(document["type"]);
    }

    // The entry point for a generator of your own. ToJsonSchema is built on exactly this and has no
    // more access to a schema than any caller now does.
    [Fact]
    public void A_Schema_Describes_Itself_For_A_Generator_That_Is_Not_Ours()
    {
        var described = Theo.Object<Address>()
            .Field(x => x.Street, Theo.String().MinLength(3))
            .Field(x => x.ZipCode, Theo.String().Length(8))
            .Describe();

        Assert.Equal(SchemaKind.Object, described.Root.Kind);
        Assert.Empty(described.Definitions);

        var properties = described.Root.Properties!;
        Assert.Equal(["Street", "ZipCode"], properties.Select(static p => p.Name));
        Assert.Equal(3, properties[0].Schema.MinLength);
        Assert.True(properties[0].IsRequired);
    }

    // A schema that contains itself is described once and referred to by name, which is both how a
    // document writes it down and what keeps the result finite.
    [Fact]
    public void A_Recursive_Schema_Is_Described_Once_And_Referred_To()
    {
        var described = CommentSchema.Describe();

        Assert.Equal("#/$defs/Comment", described.Root.Reference);

        var definition = Assert.Contains("Comment", described.Definitions);
        Assert.Equal("#/$defs/Comment", definition.Properties![1].Schema.Items!.Reference);
    }

    [Fact]
    public void A_Generator_Chooses_How_A_Reference_Is_Spelled()
    {
        var described = CommentSchema.Describe(referencePrefix: string.Empty);

        Assert.Equal("Comment", described.Root.Reference);
    }

    [Fact]
    public void A_Generator_Chooses_Which_Side_To_Describe()
    {
        var schema = Theo.String().Transform(static text => text.Length, Theo.Int().Min(1));

        Assert.Equal(SchemaKind.String, schema.Describe().Root.Kind);
        Assert.Equal(SchemaKind.Integer, schema.Describe(DescriptionDirection.Output).Root.Kind);
    }

    // The direction a custom schema is being asked about reaches it through the context, which is the
    // second and last thing that type promises.
    [Fact]
    public void A_Custom_Schema_Can_Read_The_Direction_It_Is_Being_Asked_About()
    {
        var seen = new List<DescriptionDirection>();
        var schema = new DirectionReportingSchema(seen);

        schema.ToJsonSchema();
        schema.ToJsonSchema(new JsonSchemaOptions { Direction = DescriptionDirection.Output });

        Assert.Equal([DescriptionDirection.Input, DescriptionDirection.Output], seen);
    }

    // An amendment gets the structured facts as well as the rendering, which is what lets it put back
    // something the dialect had nowhere for -- a temporal bound, for instance.
    [Fact]
    public void An_Amendment_Is_Given_The_Description_The_Node_Was_Written_From()
    {
        object? minimum = null;

        Theo.DateOnly().Min(new DateOnly(2026, 1, 1)).ToJsonSchema(new JsonSchemaOptions
        {
            Amend = node => minimum = node.Description.Minimum,
        });

        Assert.Equal(new DateOnly(2026, 1, 1), minimum);
    }

    // The copy constructor is how every wrapper schema changes one thing about what its inner schema
    // said, so a property it forgets is a constraint that silently disappears from every document with
    // a nullable, defaulted or annotated value in it. Reflection rather than a hand-written list,
    // because a hand-written list is the thing that gets forgotten.
    [Fact]
    public void The_Copy_Constructor_Carries_Every_Property()
    {
        var properties = typeof(SchemaDescription)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var original = new SchemaDescription();

        foreach (var property in properties)
        {
            property.SetValue(original, Distinctive(property.PropertyType));
        }

        var copy = new SchemaDescription(original);

        foreach (var property in properties)
        {
            Assert.Equal(property.GetValue(original), property.GetValue(copy));
        }
    }

    [Fact]
    public void A_Property_Description_Refuses_A_Missing_Name() =>
        Assert.Throws<ArgumentException>(() =>
            new PropertyDescription(string.Empty, new SchemaDescription(), isRequired: true));

    [Fact]
    public void A_Property_Description_Refuses_A_Missing_Schema() =>
        Assert.Throws<ArgumentNullException>(() =>
            new PropertyDescription("Name", null!, isRequired: true));

    [Fact]
    public void Describing_Refuses_A_Null_Schema() =>
        Assert.Throws<ArgumentNullException>(() =>
            SchemaDescriptionExtensions.Describe<string, string>(null!));

    // A value that is not the type's default, so that a property the copy constructor forgot shows up
    // as a difference rather than as two defaults matching.
    private static object Distinctive(Type type)
    {
        if (type == typeof(SchemaKind))
        {
            return SchemaKind.Map;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(int?))
        {
            return 7;
        }

        if (type == typeof(string) || type == typeof(object))
        {
            return "distinctive";
        }

        if (type == typeof(SchemaDescription))
        {
            return new SchemaDescription { Format = "distinctive" };
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            var element = type.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;

            list.Add(element == typeof(PropertyDescription)
                ? new PropertyDescription("Distinctive", new SchemaDescription(), isRequired: true)
                : Distinctive(element));

            return list;
        }

        throw new NotSupportedException(
            $"SchemaDescription gained a property of type {type}, which this test does not know how " +
            "to produce a distinctive value for. Teach it, rather than dropping the property: the " +
            "point of this test is that nothing is left out of the copy constructor.");
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
