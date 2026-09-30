using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace Theon.AspNetCore.Tests;

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }
}

/// <summary>
/// A real ASP.NET Core application, served in memory.
/// </summary>
/// <remarks>
/// The filter is exercised through the actual pipeline rather than called directly, because what
/// is worth proving is the behaviour a client sees: the status code, the response body, and
/// whether the handler ran at all.
/// </remarks>
internal sealed class TestApplication : IAsyncDisposable
{
    private static readonly HashSet<string> Taken = new(StringComparer.OrdinalIgnoreCase)
    {
        "taken@example.com",
    };

    internal static readonly Schema<CreateUserRequest> UserSchema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().Trim().MinLength(3).MaxLength(50))
            .Field(x => x.Email, Theo.String().Trim().ToLowerInvariant().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120));

    internal static readonly Schema<CreateUserRequest> UniqueEmailSchema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Email, Theo.String().Email()
                .RefineAsync(
                    (email, _) => ValueTask.FromResult(!Taken.Contains(email)),
                    "That address is already registered."));

    private readonly WebApplication _app;

    private TestApplication(WebApplication app) => _app = app;

    internal HttpClient Client { get; private set; } = null!;

    /// <summary>Gets the number of times a guarded handler actually ran.</summary>
    internal static int HandlerCalls { get; private set; }

    internal static async Task<TestApplication> StartAsync()
    {
        HandlerCalls = 0;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var app = builder.Build();

        app.MapPost("/users", (CreateUserRequest request) =>
        {
            HandlerCalls++;
            return TypedResults.Ok(request.Email);
        }).Validate(UserSchema);

        app.MapPost("/unique", (CreateUserRequest request) =>
        {
            HandlerCalls++;
            return TypedResults.Ok(request.Email);
        }).Validate(UniqueEmailSchema);

        app.MapPost("/mismatched", (string plain) => TypedResults.Ok(plain))
           .Validate(UserSchema);

        await app.StartAsync();

        return new TestApplication(app) { Client = app.GetTestClient() };
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        await _app.DisposeAsync();
    }
}
