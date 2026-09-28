using BenchmarkDotNet.Attributes;

namespace Theon.Benchmarks;

[MemoryDiagnoser]
public class ParsingBenchmarks
{
    private static readonly Schema<string> SimpleString =
        Theo.String().MinLength(3).MaxLength(100);

    private static readonly Schema<string> TransformingString =
        Theo.String().Trim().ToLowerInvariant().MinLength(3).Email();

    private static readonly Schema<string> RefinedString =
        Theo.String().MinLength(3).Refine(static v => !v.Contains(' ', StringComparison.Ordinal), "No spaces.");

    private static readonly Schema<CreateUserRequest> FlatObject =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty());

    private static readonly Schema<CreateUserRequest> NestedObject =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty())
            .Field(x => x.Address, Theo.Object<Address>()
                .Field(a => a.Street, Theo.String().MinLength(3))
                .Field(a => a.City, Theo.String().MinLength(2))
                .Field(a => a.ZipCode, Theo.String().Length(8)));

    private CreateUserRequest _valid = null!;
    private CreateUserRequest _firstFieldInvalid = null!;
    private CreateUserRequest _allFieldsInvalid = null!;

    [GlobalSetup]
    public void Setup()
    {
        _valid = new CreateUserRequest
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Age = 36,
            CompanyId = Guid.NewGuid(),
            Address = new Address { Street = "Main Street", City = "London", ZipCode = "12345678" },
        };

        _firstFieldInvalid = new CreateUserRequest
        {
            Name = "A",
            Email = "ada@example.com",
            Age = 36,
            CompanyId = Guid.NewGuid(),
            Address = new Address { Street = "Main Street", City = "London", ZipCode = "12345678" },
        };

        _allFieldsInvalid = new CreateUserRequest
        {
            Name = "A",
            Email = "nope",
            Age = 5,
            CompanyId = Guid.Empty,
            Address = new Address(),
        };
    }

    [Benchmark(Baseline = true)]
    public bool String_Simple() => SimpleString.SafeParse("a reasonable value").IsSuccess;

    [Benchmark]
    public bool String_WithTransforms() => TransformingString.SafeParse("  ADA@Example.com  ").IsSuccess;

    [Benchmark]
    public bool String_WithRefinement() => RefinedString.SafeParse("no-spaces-here").IsSuccess;

    [Benchmark]
    public bool Object_Flat_Valid() => FlatObject.SafeParse(_valid).IsSuccess;

    [Benchmark]
    public bool Object_Nested_Valid() => NestedObject.SafeParse(_valid).IsSuccess;

    [Benchmark]
    public bool Object_FirstFieldInvalid() => FlatObject.SafeParse(_firstFieldInvalid).IsSuccess;

    [Benchmark]
    public int Object_AllFieldsInvalid() => FlatObject.SafeParse(_allFieldsInvalid).Errors.Count;

    [Benchmark]
    public int Object_Nested_AllInvalid() => NestedObject.SafeParse(_allFieldsInvalid).Errors.Count;

    [Benchmark]
    public bool Object_IsValid_FailFast() => FlatObject.IsValid(_allFieldsInvalid);
}
