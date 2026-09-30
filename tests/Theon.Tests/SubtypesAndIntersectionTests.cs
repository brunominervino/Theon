using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// A discriminated union dispatched on the type, and two schemas required at once.
/// </summary>
public class SubtypesAndIntersectionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Schema<Payment> PaymentSchema() =>
        Theo.Subtypes<Payment>()
            .Case(Theo.Object<PixPayment>()
                .Field(x => x.Amount, Theo.Decimal().Positive())
                .Field(x => x.Key, Theo.String().NotEmpty()))
            .Case(Theo.Object<CardPayment>()
                .Field(x => x.Amount, Theo.Decimal().Positive())
                .Field(x => x.Number, Theo.String().Length(16))
                .Field(x => x.Holder, Theo.String().NotEmpty()));

    [Fact]
    public void Each_Subtype_Goes_Down_Its_Own_Branch()
    {
        var schema = PaymentSchema();

        Assert.True(schema.IsValid(new PixPayment { Amount = 10m, Key = "ada@example.com" }));
        Assert.True(schema.IsValid(new CardPayment
        {
            Amount = 10m,
            Number = "4111111111111111",
            Holder = "Ada",
        }));
    }

    // The error a list of alternatives cannot give. "Key: a value is required" names the branch's own
    // field, rather than saying the value was none of three things the caller never chose between.
    [Fact]
    public void The_Error_Comes_From_The_Branch_That_Matched()
    {
        var result = PaymentSchema().SafeParse(new PixPayment { Amount = 10m, Key = "" });

        var error = Assert.Single(result.Errors);
        Assert.Equal("Key", error.Path.ToString());
        Assert.Equal(ValidationErrorCode.TooSmall, error.Code);
    }

    [Fact]
    public void Every_Failure_Within_The_Branch_Is_Reported()
    {
        var result = PaymentSchema().SafeParse(new CardPayment { Amount = 0m, Number = "4", Holder = "" });

        Assert.Equal(["Amount", "Number", "Holder"], result.Errors.Select(e => e.Path.ToString()));
    }

    // The union is closed: a subtype with no branch is an error, not a value that quietly passes.
    [Fact]
    public void A_Subtype_With_No_Branch_Is_Refused()
    {
        var result = PaymentSchema().SafeParse(new BoletoPayment { Amount = 10m, Barcode = "x" });

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("PixPayment, CardPayment", error.Info.Expected);
        Assert.Equal("BoletoPayment", error.Info.Received);
    }

    [Fact]
    public void WithMessage_Replaces_The_Unmatched_Message()
    {
        var schema = Theo.Subtypes<Payment>()
            .Case(Theo.Object<PixPayment>())
            .WithMessage("We do not take that kind of payment.");

        var result = schema.SafeParse(new BoletoPayment());

        Assert.Equal("We do not take that kind of payment.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Null_Is_Refused_And_AllowNull_Accepts_It()
    {
        Assert.False(PaymentSchema().IsValid(null!));
        Assert.True(Theo.Subtypes<Payment>().Case(Theo.Object<PixPayment>()).AllowNull().IsValid(null));
    }

    [Fact]
    public void A_Subtype_Cannot_Have_Two_Branches()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Theo.Subtypes<Payment>()
                .Case(Theo.Object<PixPayment>())
                .Case(Theo.Object<PixPayment>()));

        Assert.Contains("already has a branch", exception.Message, StringComparison.Ordinal);
    }

    // The edge case that earned the check. Branches are tried in order, so a branch for a base type
    // declared first swallows every subtype after it, and the later rule silently never runs. Failing
    // when the schema is built is the only place that is cheap to notice.
    [Fact]
    public void A_Branch_That_Could_Never_Run_Is_Refused_When_The_Schema_Is_Built()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Theo.Subtypes<IInstrument>()
                .Case(Theo.Object<IInstrument>())
                .Case(Theo.Object<Wire>()));

        Assert.Contains("would never be reached", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Interface_Works_As_The_Base()
    {
        var schema = Theo.Subtypes<IInstrument>()
            .Case(Theo.Object<Wire>().Field(x => x.Label, Theo.String().NotEmpty()));

        Assert.True(schema.IsValid(new Wire { Label = "swift" }));
        Assert.False(schema.IsValid(new Wire { Label = "" }));
    }

    [Fact]
    public void A_Union_Nested_In_A_Field_Reports_Through_The_Field()
    {
        var schema = Theo.Object<Order>().Field(x => x.Payment, PaymentSchema());

        var result = schema.SafeParse(new Order { Payment = new PixPayment { Amount = 10m, Key = "" } });

        Assert.Equal("Payment.Key", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public async Task A_Union_Carries_Asynchrony_Into_Its_Branch()
    {
        var schema = Theo.Subtypes<Payment>()
            .Case(Theo.Object<PixPayment>()
                .Field(x => x.Key, Theo.String().RefineAsync(
                    static (value, _) => new ValueTask<bool>(value != "taken"),
                    "Already registered.")));

        var free = new PixPayment { Key = "free" };
        var taken = new PixPayment { Key = "taken" };

        Assert.True((await schema.SafeParseAsync(free, cancellationToken: Ct)).IsSuccess);
        Assert.False((await schema.SafeParseAsync(taken, cancellationToken: Ct)).IsSuccess);
    }

    [Fact]
    public async Task An_Unmatched_Subtype_Is_Refused_On_The_Asynchronous_Path_Too()
    {
        var result = await PaymentSchema().SafeParseAsync(new BoletoPayment(), cancellationToken: Ct);

        Assert.Equal(ValidationErrorCode.InvalidType, Assert.Single(result.Errors).Code);
    }

    // The flat model needs no new API. Where the discriminator really is a property — because the
    // service does not use polymorphic serialization — When already applies a block of rules on a
    // condition, and that is the tool for it.
    [Fact]
    public void A_Flat_Model_With_A_Kind_Property_Is_A_Job_For_When()
    {
        var schema = Theo.Object<FlatPayment>()
            .Field(x => x.Kind, Theo.OneOf("Unknown kind.", Theo.Literal("pix"), Theo.Literal("card")))
            .When(x => x.Kind == "pix", rules => rules
                .Field(x => x.PixKey, Theo.String().NotEmpty().Required()))
            .When(x => x.Kind == "card", rules => rules
                .Field(x => x.CardNumber, Theo.String().Length(16).Required()));

        Assert.True(schema.IsValid(new FlatPayment { Kind = "pix", PixKey = "ada@example.com" }));
        Assert.False(schema.IsValid(new FlatPayment { Kind = "pix" }));
        Assert.False(schema.IsValid(new FlatPayment { Kind = "card", PixKey = "ada@example.com" }));

        var result = schema.SafeParse(new FlatPayment { Kind = "pix" });
        Assert.Equal("PixKey", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void And_Requires_Both_Schemas()
    {
        var schema = Theo.String().MaxLength(10).And(Theo.String().StartsWith("acme-"));

        Assert.True(schema.IsValid("acme-1"));
        Assert.False(schema.IsValid("other"));
        Assert.False(schema.IsValid("acme-much-too-long"));
    }

    // Both see the same input and both report, which is what makes it an intersection rather than a
    // chain: a caller sees every reason at once.
    [Fact]
    public void And_Reports_From_Both_Sides()
    {
        var result = Theo.String().MaxLength(3).And(Theo.String().StartsWith("acme-")).SafeParse("other");

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(
            [ValidationErrorCode.TooBig, ValidationErrorCode.InvalidFormat],
            result.Errors.Select(e => e.Code));
    }

    // Only one output can be kept, and it is the left one's. A schema that normalizes belongs there.
    [Fact]
    public void And_Keeps_The_Left_Schemas_Value()
    {
        var schema = Theo.String().Trim().And(Theo.String().MinLength(1));

        Assert.Equal("ada", schema.Parse("  ada  "));
    }

    [Fact]
    public void And_Chains()
    {
        var schema = Theo.Int().Min(1).And(Theo.Int().Max(100)).And(Theo.Int().MultipleOf(5));

        Assert.True(schema.IsValid(50));
        Assert.False(schema.IsValid(52));

        // Zero is under the minimum, and is also both within the maximum and a multiple of five, so
        // exactly one of the three has anything to say about it.
        Assert.Single(schema.SafeParse(0).Errors);

        // A value past the maximum that is not a multiple reaches two of the three.
        Assert.Equal(2, schema.SafeParse(102).Errors.Count);
    }

    [Fact]
    public void And_Works_On_Objects()
    {
        var platform = Theo.Object<CreateUserRequest>().Field(x => x.Name, Theo.String().MinLength(3));
        var tenant = Theo.Object<CreateUserRequest>().Field(x => x.Email, Theo.String().EndsWith("@acme.com"));

        var schema = platform.And(tenant);

        Assert.True(schema.IsValid(new CreateUserRequest { Name = "Ada", Email = "ada@acme.com" }));

        var result = schema.SafeParse(new CreateUserRequest { Name = "A", Email = "ada@other.com" });
        Assert.Equal(["Name", "Email"], result.Errors.Select(e => e.Path.ToString()));
    }

    [Fact]
    public async Task And_Runs_Both_Sides_On_The_Asynchronous_Path()
    {
        var schema = Theo.String()
            .RefineAsync(static (v, _) => new ValueTask<bool>(v.Length > 2), "Too short.")
            .And(Theo.String().StartsWith("acme-"));

        Assert.True((await schema.SafeParseAsync("acme-1", cancellationToken: Ct)).IsSuccess);

        var result = await schema.SafeParseAsync("x", cancellationToken: Ct);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void And_Refuses_A_Null_Operand() =>
        Assert.Throws<ArgumentNullException>(() => Theo.String().And(null!));

    [Fact]
    public void And_Respects_StopOnFirstError()
    {
        var schema = Theo.String().MaxLength(3).And(Theo.String().StartsWith("acme-"));

        var result = schema.SafeParse("other", new ParseOptions { StopOnFirstError = true });

        Assert.Single(result.Errors);
    }
}
