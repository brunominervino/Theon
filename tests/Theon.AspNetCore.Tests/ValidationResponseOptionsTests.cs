using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Theon.AspNetCore.Tests;

/// <summary>
/// What a failure answers with.
/// </summary>
/// <remarks>
/// It was 400 with a message per path and nothing else: no way to answer 422, no code a client could
/// branch on, and no code on the individual errors — so a client had to match on message text, which
/// this library says everywhere else is not a contract.
/// </remarks>
public class ValidationResponseOptionsTests
{
    private static readonly Schema<CreateUserRequest> Schema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Age, Theo.Int().Min(18));

    private static readonly CreateUserRequest Bad = new() { Name = "a", Age = 1 };

    private static async Task<(HttpStatusCode Status, JsonNode Body)> PostAsync(
        Action<IServiceCollection>? services = null,
        Action<TheonValidationOptions>? perEndpoint = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        services?.Invoke(builder.Services);

        var app = builder.Build();
        var endpoint = app.MapPost("/users", (CreateUserRequest _) => TypedResults.Ok());

        if (perEndpoint is null)
        {
            endpoint.Validate(Schema);
        }
        else
        {
            endpoint.Validate(Schema, perEndpoint);
        }

        await app.StartAsync();

        try
        {
            using var client = app.GetTestClient();
            var response = await client.PostAsJsonAsync("/users", Bad, TestContext.Current.CancellationToken);
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            return (response.StatusCode, JsonNode.Parse(text)!);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // Nothing configured answers what it always answered, which is the whole compatibility promise.
    [Fact]
    public async Task Configuring_Nothing_Answers_What_It_Always_Did()
    {
        var (status, body) = await PostAsync();

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(400, body["status"]!.GetValue<int>());
        Assert.Equal(
            "Must be at least 3 character(s) long.",
            body["errors"]!["Name"]!.AsArray()[0]!.GetValue<string>());
    }

    [Fact]
    public async Task The_Status_Is_The_Applications_To_Choose()
    {
        var (status, body) = await PostAsync(services => services.AddTheon(options =>
            options.Status = StatusCodes.Status422UnprocessableEntity));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(422, body["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task So_Are_The_Type_The_Title_And_A_Code()
    {
        var (_, body) = await PostAsync(services => services.AddTheon(options =>
        {
            options.Type = "https://nucleo.example/erros/validacao";
            options.Title = "Falha de validacao";
            options.Code = "validacao";
        }));

        Assert.Equal("https://nucleo.example/erros/validacao", body["type"]!.GetValue<string>());
        Assert.Equal("Falha de validacao", body["title"]!.GetValue<string>());
        Assert.Equal("validacao", body["code"]!.GetValue<string>());
    }

    // The errors keep their default shape until asked, because changing it is a break for anything
    // already reading the response.
    [Fact]
    public async Task Each_Error_Can_Carry_Its_Code()
    {
        var (_, body) = await PostAsync(services => services.AddTheon(options =>
            options.IncludeErrorCodes = true));

        var name = body["errors"]!["Name"]!.AsArray()[0]!;

        Assert.Equal("Must be at least 3 character(s) long.", name["message"]!.GetValue<string>());
        Assert.Equal("TooSmall", name["code"]!.GetValue<string>());
    }

    // The point of carrying codes: a rule that named itself is the rule a client wants to recognise.
    [Fact]
    public async Task A_Rule_That_Named_Itself_Reports_That_Name()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Refine(
                static value => value.Name.Length == 14,
                "Informe um CNPJ valido.",
                "cnpj.digito_verificador");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddTheon(options => options.IncludeErrorCodes = true);

        var app = builder.Build();
        app.MapPost("/cnpj", (CreateUserRequest _) => TypedResults.Ok()).Validate(schema);
        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var client = app.GetTestClient();
            var response = await client.PostAsJsonAsync("/cnpj", Bad, TestContext.Current.CancellationToken);
            var body = JsonNode.Parse(
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;

            var error = body["errors"]![""]!.AsArray()[0]!;

            Assert.Equal("cnpj.digito_verificador", error["code"]!.GetValue<string>());
            Assert.Equal("Informe um CNPJ valido.", error["message"]!.GetValue<string>());
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // The paths keying errors are what a form matches against the body it sent.
    [Fact]
    public async Task The_Paths_Take_The_Naming_Policy()
    {
        var (_, body) = await PostAsync(services => services.AddTheon(options =>
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase));

        Assert.NotNull(body["errors"]!["name"]);
        Assert.Null(body["errors"]!["Name"]);
    }

    [Fact]
    public async Task An_Endpoint_Can_Answer_Differently_From_The_Rest()
    {
        var (status, _) = await PostAsync(
            services => services.AddTheon(options => options.Status = 400),
            perEndpoint: options => options.Status = StatusCodes.Status422UnprocessableEntity);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
    }

    [Fact]
    public async Task AddTheon_With_Nothing_To_Configure_Is_Allowed()
    {
        var (status, _) = await PostAsync(services => services.AddTheon());

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public void AddTheon_Refuses_A_Null_Collection() =>
        Assert.Throws<ArgumentNullException>(() =>
            TheonServiceCollectionExtensions.AddTheon(null!));

    [Fact]
    public void Validate_Refuses_A_Null_Configuration()
    {
        var app = WebApplication.CreateSlimBuilder().Build();
        var endpoint = app.MapPost("/x", (CreateUserRequest _) => TypedResults.Ok());

        Assert.Throws<ArgumentNullException>(() => endpoint.Validate(Schema, null!));
    }
}
