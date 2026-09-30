using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// Rules that cannot be answered from the value alone, and need a round trip to decide.
/// </summary>
public class AsyncValidationTests
{
    private static readonly HashSet<string> Taken = new(StringComparer.OrdinalIgnoreCase)
    {
        "ada@example.com",
        "grace@example.com",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ValueTask<bool> IsAvailable(string email, CancellationToken ct) =>
        ValueTask.FromResult(!Taken.Contains(email));

    private static readonly Schema<string> UniqueEmail =
        Theo.String().Trim().ToLowerInvariant().Email()
            .RefineAsync(IsAvailable, "That address is already registered.");

    [Fact]
    public async Task An_Available_Address_Passes()
    {
        var result = await UniqueEmail.SafeParseAsync("  New@Example.com  ", cancellationToken: Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.com", result.Value);
    }

    [Fact]
    public async Task A_Taken_Address_Fails()
    {
        var result = await UniqueEmail.SafeParseAsync("ada@example.com", cancellationToken: Ct);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.Custom, error.Code);
        Assert.Equal("That address is already registered.", error.Message);
    }

    [Fact]
    public async Task The_Synchronous_Rules_Run_First_And_Spare_The_Round_Trip()
    {
        var calls = 0;
        var schema = Theo.String().Email().RefineAsync(
            (_, _) => { calls++; return ValueTask.FromResult(true); },
            "unused");

        var result = await schema.SafeParseAsync("not-an-email", cancellationToken: Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Parsing_An_Async_Schema_Synchronously_Throws_Rather_Than_Blocking()
    {
        // Blocking on I/O inside a request handler is how thread-pool starvation starts. The
        // synchronous path refuses instead of quietly appearing to work.
        Assert.Throws<SchemaAsyncUsageException>(() => UniqueEmail.Parse("someone@example.com"));
        Assert.Throws<SchemaAsyncUsageException>(() => UniqueEmail.IsValid("someone@example.com"));
    }

    [Fact]
    public async Task A_Fully_Synchronous_Schema_Can_Still_Be_Awaited()
    {
        var schema = Theo.String().MinLength(3);

        Assert.True((await schema.SafeParseAsync("abc", cancellationToken: Ct)).IsSuccess);
        Assert.False((await schema.SafeParseAsync("ab", cancellationToken: Ct)).IsSuccess);
    }

    [Fact]
    public async Task An_Async_Rule_Inside_An_Object_Reports_On_Its_Field()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().NotEmpty())
            .Field(x => x.Email, UniqueEmail);

        var result = await schema.SafeParseAsync(new CreateUserRequest
        {
            Name = "Ada",
            Email = "ada@example.com",
        }, cancellationToken: Ct);

        Assert.Equal("Email", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public async Task Synchronous_And_Asynchronous_Failures_Are_Collected_Together()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Email, UniqueEmail);

        var result = await schema.SafeParseAsync(new CreateUserRequest
        {
            Name = "A",
            Email = "grace@example.com",
        }, cancellationToken: Ct);

        Assert.Equal(
            new[] { "Name", "Email" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public async Task An_Async_Rule_Inside_A_Collection_Reports_By_Index()
    {
        var schema = Theo.Collection(UniqueEmail);

        var result = await schema.SafeParseAsync(new[] { "free@example.com", "ada@example.com" }, cancellationToken: Ct);

        Assert.Equal("[1]", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public async Task Nesting_Carries_The_Whole_Path()
    {
        var schema = Theo.Object<Mailing>()
            .Field(x => x.Recipients, Theo.Collection(UniqueEmail));

        var result = await schema.SafeParseAsync(new Mailing
        {
            Recipients = ["ok@example.com", "grace@example.com"],
        }, cancellationToken: Ct);

        Assert.Equal("Recipients[1]", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public async Task An_Async_Rule_Survives_AllowNull()
    {
        var schema = UniqueEmail.AllowNull();

        Assert.True((await schema.SafeParseAsync(null, cancellationToken: Ct)).IsSuccess);
        Assert.False((await schema.SafeParseAsync("ada@example.com", cancellationToken: Ct)).IsSuccess);
    }

    [Fact]
    public async Task ParseAsync_Throws_With_The_Errors_Attached()
    {
        var exception = await Assert.ThrowsAsync<SchemaValidationException>(
            async () => await UniqueEmail.ParseAsync("ada@example.com", cancellationToken: Ct));

        Assert.Single(exception.Errors);
    }

    [Fact]
    public async Task Cancellation_Reaches_The_Rule()
    {
        using var cts = new CancellationTokenSource();
        var schema = Theo.String().RefineAsync(
            (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return ValueTask.FromResult(true);
            },
            "unused");

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await schema.SafeParseAsync("value", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task StopOnFirstError_Applies_To_The_Async_Path_Too()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Email, UniqueEmail);

        var result = await schema.SafeParseAsync(
            new CreateUserRequest { Name = "A", Email = "ada@example.com" },
            new ParseOptions { StopOnFirstError = true },
            Ct);

        Assert.Single(result.Errors);
    }
}
