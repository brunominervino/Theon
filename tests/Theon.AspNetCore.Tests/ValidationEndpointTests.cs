using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Theon.AspNetCore.Tests;

public class ValidationEndpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateUserRequest Valid() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Age = 36,
    };

    private static async Task<JsonElement> ReadErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return body.GetProperty("errors");
    }

    [Fact]
    public async Task A_Valid_Body_Reaches_The_Handler()
    {
        await using var app = await TestApplication.StartAsync();

        var response = await app.Client.PostAsJsonAsync("/users", Valid(), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, TestApplication.HandlerCalls);
    }

    [Fact]
    public async Task An_Invalid_Body_Is_Rejected_And_The_Handler_Never_Runs()
    {
        await using var app = await TestApplication.StartAsync();

        var response = await app.Client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest { Name = "A", Email = "nope", Age = 5 },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, TestApplication.HandlerCalls);
    }

    [Fact]
    public async Task The_Response_Is_A_Validation_Problem()
    {
        await using var app = await TestApplication.StartAsync();

        var response = await app.Client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest { Name = "A", Email = "nope", Age = 5 },
            Ct);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Every_Failing_Field_Appears_Keyed_By_Its_Name()
    {
        await using var app = await TestApplication.StartAsync();

        var response = await app.Client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest { Name = "A", Email = "nope", Age = 5 },
            Ct);

        var errors = await ReadErrorsAsync(response);

        Assert.Equal("Must be at least 3 character(s) long.", errors.GetProperty("Name")[0].GetString());
        Assert.Equal("Invalid e-mail address.", errors.GetProperty("Email")[0].GetString());
        Assert.Equal("Must be greater than or equal to 18.", errors.GetProperty("Age")[0].GetString());
    }

    [Fact]
    public async Task One_Failing_Field_Reports_Only_Itself()
    {
        await using var app = await TestApplication.StartAsync();

        var request = Valid();
        request.Age = 200;

        var errors = await ReadErrorsAsync(await app.Client.PostAsJsonAsync("/users", request, Ct));

        Assert.Single(errors.EnumerateObject());
        Assert.Equal("Must be less than or equal to 120.", errors.GetProperty("Age")[0].GetString());
    }

    [Fact]
    public async Task An_Asynchronous_Rule_Runs_Inside_The_Request()
    {
        await using var app = await TestApplication.StartAsync();

        var taken = await app.Client.PostAsJsonAsync(
            "/unique",
            new CreateUserRequest { Email = "taken@example.com" },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal(
            "That address is already registered.",
            (await ReadErrorsAsync(taken)).GetProperty("Email")[0].GetString());

        var free = await app.Client.PostAsJsonAsync(
            "/unique",
            new CreateUserRequest { Email = "free@example.com" },
            Ct);

        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
    }

    [Fact]
    public async Task A_Missing_Body_Is_Rejected_Rather_Than_Crashing()
    {
        await using var app = await TestApplication.StartAsync();

        using var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
        var response = await app.Client.PostAsync("/users", content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, TestApplication.HandlerCalls);
    }

    [Fact]
    public async Task Validating_A_Type_The_Handler_Does_Not_Accept_Says_So_Plainly()
    {
        // A wiring mistake: the endpoint takes a string, the filter guards a CreateUserRequest.
        // Better a clear failure than silently validating a default and rejecting every request.
        await using var app = await TestApplication.StartAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await app.Client.PostAsJsonAsync("/mismatched?plain=hello", "hello", Ct));

        Assert.Contains("CreateUserRequest", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Normalisation_Does_Not_Reach_The_Handler()
    {
        // Trim and ToLowerInvariant run during validation, but the filter validates the model the
        // binder produced and does not replace it. The handler sees what the client sent.
        await using var app = await TestApplication.StartAsync();

        var request = Valid();
        request.Email = "  ADA@Example.COM  ";

        var response = await app.Client.PostAsJsonAsync("/users", request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("  ADA@Example.COM  ", await response.Content.ReadFromJsonAsync<string>(Ct));
    }
}
