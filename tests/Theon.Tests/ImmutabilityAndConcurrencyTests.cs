using System.Collections.Concurrent;

namespace Theon.Tests;

/// <summary>
/// A schema is meant to be built once, stored in a static field, and used from every request
/// thread at the same time. These tests hold that promise to account.
/// </summary>
public class ImmutabilityAndConcurrencyTests
{
    [Fact]
    public void Adding_A_Rule_Returns_A_New_Schema_And_Leaves_The_Original_Alone()
    {
        var lenient = Theo.String();
        var strict = lenient.MinLength(5);

        Assert.NotSame(lenient, strict);
        Assert.True(lenient.IsValid("ab"));
        Assert.False(strict.IsValid("ab"));
    }

    [Fact]
    public void A_Shared_Schema_Is_Not_Disturbed_By_Deriving_From_It()
    {
        var shared = Theo.String().MinLength(3);
        _ = shared.MaxLength(5);
        _ = shared.Email();

        Assert.True(shared.IsValid("a much longer value than five"));
    }

    [Fact]
    public async Task Parallel_Parses_Of_One_Schema_Do_Not_Interfere()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().Trim().MinLength(3))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Age, Theo.Int().Min(18));

        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 2_000),
            (i, _) =>
            {
                var shouldPass = i % 2 == 0;
                var request = new CreateUserRequest
                {
                    Name = shouldPass ? "Valid Name" : "X",
                    Email = shouldPass ? "ok@example.com" : "bad",
                    Age = shouldPass ? 30 : 1,
                };

                var result = schema.SafeParse(request);

                if (result.IsSuccess != shouldPass)
                {
                    failures.Add($"iteration {i} expected {shouldPass}");
                }

                if (!shouldPass && result.Errors.Count != 3)
                {
                    failures.Add($"iteration {i} produced {result.Errors.Count} errors, expected 3");
                }

                return ValueTask.CompletedTask;
            });

        Assert.Empty(failures);
    }

    [Fact]
    public void Errors_From_One_Parse_Do_Not_Leak_Into_The_Next()
    {
        var schema = Theo.String().MinLength(5);

        Assert.Single(schema.SafeParse("a").Errors);
        Assert.Empty(schema.SafeParse("abcde").Errors);
        Assert.Single(schema.SafeParse("b").Errors);
    }
}
