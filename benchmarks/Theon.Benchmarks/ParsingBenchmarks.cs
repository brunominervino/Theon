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

    private static readonly Schema<IReadOnlyList<string>> EmailList =
        Theo.Collection(Theo.String().Email()).MinCount(1).MaxCount(100);

    private static readonly Schema<UserStatus> Status = Theo.Enum<UserStatus>();

    private static readonly Schema<DateTime> Timestamp =
        Theo.DateTime().RequireUtc().Min(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Same four fields as FlatObject, over a struct. An accessor is a Func<T, TValue>, so a
    // struct is copied once per field read; this is what that costs.
    private static readonly Schema<CreateUserValue> FlatStruct =
        Theo.Object<CreateUserValue>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty());

    private static readonly Schema<Ticket> Conditional =
        Theo.Object<Ticket>()
            .Field(x => x.Title, Theo.String().NotEmpty())
            .Field(x => x.Status, Theo.Enum<TicketStatus>())
            .When(x => x.Status == TicketStatus.Completed, rules => rules
                .Field(x => x.CompletedAt, Theo.DateTime().RequireUtc().Required())
                .Field(x => x.ClosedBy, Theo.String().NotEmpty().Required()));

    private static readonly Schema<string> UrlString = Theo.String().Url();

    private static readonly Schema<string> UuidString = Theo.String().Uuid();

    private static readonly Schema<string> Iso8601String = Theo.String().Iso8601();

    private static readonly Schema<int?, int> DefaultedInt = Theo.Int().Min(1).Max(100).Default(20);

    private static readonly Schema<int, int> CaughtInt = Theo.Int().Min(1).Max(100).Catch(20);

    private static readonly Schema<string> IntersectedString =
        Theo.String().MaxLength(100).And(Theo.String().StartsWith("acme-"));

    private static readonly Schema<Payment> Subtypes =
        Theo.Subtypes<Payment>()
            .Case(Theo.Object<PixPayment>()
                .Field(x => x.Amount, Theo.Decimal().Positive())
                .Field(x => x.Key, Theo.String().Email()))
            .Case(Theo.Object<CardPayment>()
                .Field(x => x.Amount, Theo.Decimal().Positive())
                .Field(x => x.Number, Theo.String().Length(16))
                .Field(x => x.Holder, Theo.String().NotEmpty()));

    private static readonly Schema<IReadOnlyList<string>> UniqueTags =
        Theo.Collection(Theo.String().MaxLength(24)).Unique();

    // The one shape that cannot be walked without allocating: an interface has no struct enumerator
    // to offer, and a set has no indexer to reach for instead. This benchmark exists to keep that
    // cost visible, and to keep it at one allocation.
    private static readonly Schema<IReadOnlyCollection<string>> TagSet =
        Theo.Set(Theo.String().MaxLength(24)).MaxCount(20);

    private static readonly Schema<string, int> PipelinedPageSize =
        Theo.String().Trim().TryTransform<int>(
            int.TryParse,
            "Must be a whole number.",
            Theo.Int().Min(1).Max(100));

    // A rule that reports for itself rather than answering yes or no. It costs one delegating call on
    // the success path; whether it costs an allocation is what this measures.
    private static readonly Schema<string> ContextuallyRefined =
        Theo.String().MinLength(3).Refine(static (string value, ref ParseContext context) =>
        {
            if (value.Contains(' ', StringComparison.Ordinal))
            {
                context.AddError(
                    new Theon.Errors.ValidationErrorInfo { Code = Theon.Errors.ValidationErrorCode.Custom },
                    "No spaces.");
            }
        });

    // Two formats that are scans rather than patterns, because the non-backtracking engine will not
    // build an automaton large enough for IPv6.
    private static readonly Schema<string> Ipv4String = Theo.String().Ipv4();

    private static readonly Schema<string> Ipv6String = Theo.String().Ipv6();

    // Two that are checksums rather than shapes.
    private static readonly Schema<string> CardNumber = Theo.String().CreditCard();

    private static readonly Schema<string> IbanString = Theo.String().Iban();

    private Payment _card = null!;
    private HashSet<string> _tagSet = null!;
    private string[] _tags = null!;
    private Ticket _conditionSkipped = null!;
    private Ticket _conditionMet = null!;
    private CreateUserValue _validStruct;
    private string[] _emails = null!;
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

        _emails = [.. Enumerable.Range(0, 20).Select(i => $"user{i}@example.com")];

        _tags = [.. Enumerable.Range(0, 10).Select(i => $"tag-{i}")];

        _tagSet = [.. _tags];

        _card = new CardPayment
        {
            Amount = 42.50m,
            Number = "4111111111111111",
            Holder = "Ada Lovelace",
        };

        _conditionSkipped = new Ticket { Title = "open", Status = TicketStatus.Open };

        _conditionMet = new Ticket
        {
            Title = "done",
            Status = TicketStatus.Completed,
            CompletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ClosedBy = "ada",
        };

        _validStruct = new CreateUserValue
        {
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Age = 36,
            CompanyId = Guid.NewGuid(),
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

    [Benchmark]
    public bool Object_FlatStruct_Valid() => FlatStruct.SafeParse(_validStruct).IsSuccess;

    [Benchmark]
    public bool When_ConditionFalse() => Conditional.SafeParse(_conditionSkipped).IsSuccess;

    [Benchmark]
    public bool When_ConditionTrue() => Conditional.SafeParse(_conditionMet).IsSuccess;

    [Benchmark]
    public bool Collection_20Emails_Valid() => EmailList.SafeParse(_emails).IsSuccess;

    [Benchmark]
    public bool Enum_Valid() => Status.SafeParse(UserStatus.Active).IsSuccess;

    [Benchmark]
    public bool DateTime_Valid() =>
        Timestamp.SafeParse(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).IsSuccess;

    [Benchmark]
    public bool Format_Url_Valid() => UrlString.SafeParse("https://www.example.com/a/b?q=1").IsSuccess;

    [Benchmark]
    public bool Format_Uuid_Valid() =>
        UuidString.SafeParse("6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f6").IsSuccess;

    // Not a regular expression: TryParseExact, so the calendar is checked and no pattern is added to
    // the set that has to be kept linear.
    [Benchmark]
    public bool Format_Iso8601_Valid() => Iso8601String.SafeParse("2026-09-30T14:30:00Z").IsSuccess;

    [Benchmark]
    public bool Default_ValuePresent() => DefaultedInt.SafeParse(50).IsSuccess;

    [Benchmark]
    public bool Default_ValueAbsent() => DefaultedInt.SafeParse(null).IsSuccess;

    // A caught failure runs the inner schema on a forked context, which is a stack struct, so the
    // success path should cost nothing beyond the inner parse.
    [Benchmark]
    public bool Catch_Valid() => CaughtInt.SafeParse(50).IsSuccess;

    [Benchmark]
    public bool Catch_Swallowing() => CaughtInt.SafeParse(0).IsSuccess;

    [Benchmark]
    public bool Intersection_Valid() => IntersectedString.SafeParse("acme-widget").IsSuccess;

    // The card branch is declared second, so this includes a type test that misses before the one
    // that hits.
    [Benchmark]
    public bool Subtypes_SecondBranch_Valid() => Subtypes.SafeParse(_card).IsSuccess;

    [Benchmark]
    public bool Unique_10Tags_Valid() => UniqueTags.SafeParse(_tags).IsSuccess;

    [Benchmark]
    public bool Set_10Tags_Valid() => TagSet.SafeParse(_tagSet).IsSuccess;

    [Benchmark]
    public int Pipeline_TextToNumber_Valid() => PipelinedPageSize.SafeParse("50").Value;

    [Benchmark]
    public bool Pipeline_TextToNumber_NotANumber() =>
        PipelinedPageSize.SafeParse("nope").IsSuccess;

    [Benchmark]
    public bool ContextualRefine_Valid() => ContextuallyRefined.SafeParse("no-spaces").IsSuccess;

    [Benchmark]
    public bool Format_Ipv4_Valid() => Ipv4String.SafeParse("192.168.100.200").IsSuccess;

    [Benchmark]
    public bool Format_Ipv6_Valid() => Ipv6String.SafeParse("2001:db8:85a3::8a2e:370:7334").IsSuccess;

    [Benchmark]
    public bool Format_CreditCard_Valid() => CardNumber.SafeParse("4111111111111111").IsSuccess;

    [Benchmark]
    public bool Format_Iban_Valid() => IbanString.SafeParse("DE89370400440532013000").IsSuccess;
}
