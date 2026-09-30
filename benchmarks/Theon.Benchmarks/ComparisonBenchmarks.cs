using System.ComponentModel.DataAnnotations;
using BenchmarkDotNet.Attributes;
using FluentValidation;

namespace Theon.Benchmarks;

/// <summary>
/// The same four rules, written three ways.
/// </summary>
/// <remarks>
/// <para>
/// Measuring this library against itself answers "did that change help". It does not answer "why
/// would I switch", which is the question anyone evaluating a validation library is actually asking.
/// This is that measurement.
/// </para>
/// <para>
/// The comparison is kept fair in the ways that can be controlled and honest about the ways it
/// cannot. All three validate the same four properties of the same object with the same bounds; all
/// three are built once and reused, which is how each library documents its own use; and all three
/// are measured on a value that passes, because that is what production traffic mostly is.
/// </para>
/// <para>
/// What differs and cannot be equalised is how strict the e-mail rules are.
/// <c>DataAnnotations</c> accepts anything with an at sign that is not at either end.
/// FluentValidation's <c>EmailAddress</c> is nearly as lenient by default. Theon's pattern is
/// pragmatic but genuinely restrictive, and it is also non-backtracking, which costs a little on a
/// short valid string and is the reason a hostile one cannot cost anything at all. So Theon is doing
/// slightly more work per address than either, and the numbers should be read knowing that.
/// </para>
/// <para>
/// What also differs, and is the point, is that <c>Validator.TryValidateObject</c> discovers the
/// rules by reflection on every call and materialises a list for the results, so it cannot be free
/// however fast the rules themselves are.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ComparisonBenchmarks
{
    private static readonly Schema<CreateUserRequest> TheonSchema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty());

    private static readonly CreateUserValidator FluentSchema = new();

    private CreateUserRequest _valid = null!;
    private CreateUserRequest _invalid = null!;
    private AnnotatedUser _validAnnotated = null!;
    private AnnotatedUser _invalidAnnotated = null!;
    private List<ValidationResult> _annotationResults = null!;

    [GlobalSetup]
    public void Setup()
    {
        var company = Guid.NewGuid();

        _valid = new CreateUserRequest
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Age = 36,
            CompanyId = company,
        };

        _invalid = new CreateUserRequest
        {
            Name = "A",
            Email = "nope",
            Age = 5,
            CompanyId = Guid.Empty,
        };

        _validAnnotated = new AnnotatedUser
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Age = 36,
            CompanyId = company,
        };

        _invalidAnnotated = new AnnotatedUser
        {
            Name = "A",
            Email = "nope",
            Age = 5,
            CompanyId = Guid.Empty,
        };

        // Reused across iterations so the list itself is not part of what is measured. A real caller
        // would allocate one per request; leaving it out is generous to DataAnnotations rather than
        // the reverse.
        _annotationResults = [];
    }

    [Benchmark(Baseline = true, Description = "Theon, valid")]
    public bool Theon_Valid() => TheonSchema.SafeParse(_valid).IsSuccess;

    [Benchmark(Description = "FluentValidation, valid")]
    public bool Fluent_Valid() => FluentSchema.Validate(_valid).IsValid;

    [Benchmark(Description = "DataAnnotations, valid")]
    public bool DataAnnotations_Valid()
    {
        _annotationResults.Clear();
        return Validator.TryValidateObject(
            _validAnnotated,
            new ValidationContext(_validAnnotated),
            _annotationResults,
            validateAllProperties: true);
    }

    [Benchmark(Description = "Theon, every field invalid")]
    public int Theon_Invalid() => TheonSchema.SafeParse(_invalid).Errors.Count;

    [Benchmark(Description = "FluentValidation, every field invalid")]
    public int Fluent_Invalid() => FluentSchema.Validate(_invalid).Errors.Count;

    [Benchmark(Description = "DataAnnotations, every field invalid")]
    public int DataAnnotations_Invalid()
    {
        _annotationResults.Clear();
        Validator.TryValidateObject(
            _invalidAnnotated,
            new ValidationContext(_invalidAnnotated),
            _annotationResults,
            validateAllProperties: true);

        return _annotationResults.Count;
    }

    // Asking only whether the value is acceptable, which is the cheapest question each library can
    // answer. Theon stops at the first failure when asked this way; the other two have no such mode.
    [Benchmark(Description = "Theon, yes or no only")]
    public bool Theon_IsValid() => TheonSchema.IsValid(_invalid);

    private sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserValidator()
        {
            RuleFor(static x => x.Name).NotNull().MinimumLength(3).MaximumLength(100);
            RuleFor(static x => x.Email).NotNull().EmailAddress();
            RuleFor(static x => x.Age).InclusiveBetween(18, 120);
            RuleFor(static x => x.CompanyId).NotEqual(Guid.Empty);
        }
    }
}

/// <summary>The same four rules as attributes, for <see cref="Validator"/>.</summary>
public sealed class AnnotatedUser
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Range(18, 120)]
    public int Age { get; set; }

    // DataAnnotations has nothing for "not the empty identifier", so this is the nearest equivalent
    // and it is doing less work than the other two rules it is compared against.
    [Required]
    public Guid CompanyId { get; set; }
}
