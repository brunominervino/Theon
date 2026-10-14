using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Theon.AspNetCore.Tests;

/// <summary>
/// A filter somebody else wrote, publishing the same metadata.
/// </summary>
/// <remarks>
/// The constructor was internal, so only this package could put a schema on an endpoint. An
/// application that validates its own way still wants whatever produces its OpenAPI description to
/// find the schema, and it had no way to say so.
/// </remarks>
public class ThirdPartyMetadataTests
{
    private static readonly Schema<CreateUserRequest> Schema =
        Theo.Object<CreateUserRequest>().Field(x => x.Name, Theo.String().MinLength(3));

    private static Endpoint Build(Action<RouteHandlerBuilder> configure)
    {
        var app = WebApplication.CreateSlimBuilder().Build();
        configure(app.MapPost("/users", (CreateUserRequest _) => TypedResults.Ok()));

        var sources = ((IEndpointRouteBuilder)app).DataSources;
        return sources.SelectMany(source => source.Endpoints).Single();
    }

    [Fact]
    public void A_Filter_Of_Our_Own_Can_Publish_The_Metadata()
    {
        var endpoint = Build(builder => builder.WithMetadata(TheonSchemaMetadata.For(Schema)));

        var metadata = endpoint.Metadata.GetMetadata<TheonSchemaMetadata>();

        Assert.NotNull(metadata);
        Assert.Equal(typeof(CreateUserRequest), metadata.ValidatedType);
    }

    // The same metadata, which means whatever reads it cannot tell the two apart -- that is the point.
    [Fact]
    public void It_Describes_The_Schema_The_Same_Way()
    {
        var ours = Build(builder => builder.Validate(Schema))
            .Metadata.GetMetadata<TheonSchemaMetadata>()!;

        var theirs = Build(builder => builder.WithMetadata(TheonSchemaMetadata.For(Schema)))
            .Metadata.GetMetadata<TheonSchemaMetadata>()!;

        Assert.Equal(ours.ValidatedType, theirs.ValidatedType);
        Assert.Equal(
            ours.Describe().Root.ToJsonString(),
            theirs.Describe().Root.ToJsonString());
    }

    [Fact]
    public void The_Options_Still_Reach_It()
    {
        var metadata = TheonSchemaMetadata.For(Schema);

        var described = metadata.Describe(new JsonSchemaOptions
        {
            IncludeDialect = false,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });

        Assert.Null(described.Root["$schema"]);
        Assert.NotNull(described.Root["properties"]!["name"]);
    }

    [Fact]
    public void It_Refuses_A_Null_Schema() =>
        Assert.Throws<ArgumentNullException>(() =>
            TheonSchemaMetadata.For<CreateUserRequest>(null!));
}
