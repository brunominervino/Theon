using System.Text.RegularExpressions;
using Theon.Errors;

namespace Theon.Tests;

public partial class OneOfSchemaTests
{
    [GeneratedRegex(@"^\+?[0-9]{10,15}$", RegexOptions.NonBacktracking)]
    private static partial Regex PhoneNumber();

    private static readonly Schema<string> Contact = Theo.OneOf(
        "Enter an e-mail address or a phone number.",
        Theo.String().Email(),
        Theo.String().Matches(PhoneNumber(), "phone"));

    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("+5511999998888")]
    [InlineData("11999998888")]
    public void Any_Satisfied_Alternative_Passes(string value) => Assert.True(Contact.IsValid(value));

    [Fact]
    public void Nothing_Satisfied_Reports_One_Message()
    {
        var result = Contact.SafeParse("neither");

        var error = Assert.Single(result.Errors);
        Assert.Equal("Enter an e-mail address or a phone number.", error.Message);
        Assert.Equal(ValidationErrorCode.InvalidValue, error.Code);
    }

    [Fact]
    public void A_Failed_Alternative_Leaves_No_Trace()
    {
        // The e-mail branch is tried first and fails; only the single choice error survives.
        var result = Contact.SafeParse("neither");

        Assert.Single(result.Errors);
    }

    [Fact]
    public void The_First_Match_Wins_And_Its_Transformations_Apply()
    {
        var schema = Theo.OneOf(
            "nope",
            Theo.String().Trim().Email(),
            Theo.String().MinLength(1));

        Assert.Equal("ada@example.com", schema.Parse("  ada@example.com  "));
    }

    [Fact]
    public void Composes_Inside_An_Object()
    {
        var schema = Theo.Object<CreateUserRequest>().Field(x => x.Email, Contact);

        var result = schema.SafeParse(new CreateUserRequest { Email = "neither" });

        Assert.Equal("Email", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Fewer_Than_Two_Alternatives_Is_Rejected_At_Construction() =>
        Assert.Throws<ArgumentException>(() => Theo.OneOf("x", Theo.String()));

    [Fact]
    public async Task Works_On_The_Asynchronous_Path()
    {
        var schema = Theo.OneOf(
            "Neither known nor valid.",
            Theo.String().Email(),
            Theo.String().RefineAsync((v, _) => ValueTask.FromResult(v == "magic"), "not magic"));

        var ct = TestContext.Current.CancellationToken;

        Assert.True((await schema.SafeParseAsync("ada@example.com", cancellationToken: ct)).IsSuccess);
        Assert.True((await schema.SafeParseAsync("magic", cancellationToken: ct)).IsSuccess);
        Assert.False((await schema.SafeParseAsync("other", cancellationToken: ct)).IsSuccess);
    }
}
