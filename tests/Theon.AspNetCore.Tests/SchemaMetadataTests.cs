using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Theon.AspNetCore.Tests;

/// <summary>
/// The metadata that lets whatever produces an OpenAPI description find out what an endpoint requires.
/// </summary>
public class SchemaMetadataTests
{
    [Fact]
    public void Validate_Leaves_The_Schema_Where_A_Generator_Can_Find_It()
    {
        var metadata = MetadataFor(builder => builder
            .MapPost("/users", (CreateUserRequest _) => Results.Ok())
            .Validate(TestApplication.UserSchema));

        Assert.NotNull(metadata);
        Assert.Equal(typeof(CreateUserRequest), metadata.ValidatedType);
    }

    [Fact]
    public void The_Metadata_Describes_The_Same_Rules_The_Filter_Enforces()
    {
        var metadata = MetadataFor(builder => builder
            .MapPost("/users", (CreateUserRequest _) => Results.Ok())
            .Validate(TestApplication.UserSchema));

        var described = metadata!.Describe();
        var properties = described.Root["properties"]!;

        Assert.Equal("object", described.Root["type"]!.GetValue<string>());
        Assert.Equal(3, properties["Name"]!["minLength"]!.GetValue<int>());
        Assert.Equal(50, properties["Name"]!["maxLength"]!.GetValue<int>());
        Assert.Equal("email", properties["Email"]!["format"]!.GetValue<string>());
        Assert.Equal(18, properties["Age"]!["minimum"]!.GetValue<int>());
    }

    // The point of handing the options through: an OpenAPI description keeps its shared schemas
    // somewhere of its own, and the references have to point there.
    [Fact]
    public void The_Caller_Chooses_How_References_Are_Written()
    {
        var metadata = MetadataFor(builder => builder
            .MapPost("/users", (CreateUserRequest _) => Results.Ok())
            .Validate(TestApplication.UserSchema));

        var described = metadata!.Describe(new JsonSchemaOptions
        {
            IncludeDialect = false,
            ReferencePrefix = "#/components/schemas/",
        });

        Assert.Null(described.Root["$schema"]);
    }

    [Fact]
    public void A_Group_Carries_It_Too()
    {
        var metadata = MetadataFor(builder =>
        {
            var group = builder.MapGroup("/users").Validate(TestApplication.UserSchema);
            return group.MapPost("/", (CreateUserRequest _) => Results.Ok());
        });

        Assert.NotNull(metadata);
        Assert.Equal(typeof(CreateUserRequest), metadata.ValidatedType);
    }

    [Fact]
    public void An_Endpoint_Without_Validate_Carries_Nothing()
    {
        var metadata = MetadataFor(builder =>
            builder.MapPost("/users", (CreateUserRequest _) => Results.Ok()));

        Assert.Null(metadata);
    }

    private static TheonSchemaMetadata? MetadataFor(
        Func<IEndpointRouteBuilder, IEndpointConventionBuilder> map)
    {
        var app = WebApplication.CreateBuilder().Build();
        map(app);

        var source = Assert.Single(((IEndpointRouteBuilder)app).DataSources);
        var endpoint = Assert.Single(source.Endpoints);

        return endpoint.Metadata.GetMetadata<TheonSchemaMetadata>();
    }
}
