using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// A caller-supplied rule that reports for itself, rather than answering yes or no.
/// </summary>
public class ContextualRefineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The thing the yes-or-no Refine cannot do: two distinct complaints, each against the field the
    // person has to fix.
    [Fact]
    public void One_Rule_Can_Report_More_Than_One_Thing()
    {
        var schema = Theo.Object<SignUp>().Refine(static (SignUp signUp, ref ParseContext context) =>
        {
            if (signUp.Password != signUp.PasswordConfirmation)
            {
                context.PushProperty(nameof(SignUp.PasswordConfirmation));
                context.AddError(
                    new ValidationErrorInfo { Code = ValidationErrorCode.Custom },
                    "The two passwords do not match.");
                context.Pop();
            }

            if (signUp.Password.Length < 8)
            {
                context.PushProperty(nameof(SignUp.Password));
                context.AddError(
                    new ValidationErrorInfo { Code = ValidationErrorCode.TooSmall },
                    "Use at least eight characters.");
                context.Pop();
            }
        });

        var result = schema.SafeParse(new SignUp { Password = "short", PasswordConfirmation = "other" });

        Assert.Equal(
            ["PasswordConfirmation", "Password"],
            result.Errors.Select(e => e.Path.ToString()));
        Assert.Equal(
            [ValidationErrorCode.Custom, ValidationErrorCode.TooSmall],
            result.Errors.Select(e => e.Code));
    }

    // A rule that reports nothing has accepted the value.
    [Fact]
    public void A_Rule_That_Says_Nothing_Accepts()
    {
        var schema = Theo.String().Refine(static (string _, ref ParseContext _) => { });

        Assert.True(schema.IsValid("anything"));
    }

    // The code is the caller's to choose, which is the other thing the yes-or-no form cannot do: it
    // always reports Custom, and a caller cannot branch on that.
    [Fact]
    public void The_Rule_Chooses_Its_Own_Code_And_Facts()
    {
        var schema = Theo.String().Refine(static (string value, ref ParseContext context) =>
        {
            if (value.Length > 5)
            {
                context.AddError(new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Text,
                    Maximum = 5,
                    Inclusive = true,
                });
            }
        });

        var error = Assert.Single(schema.SafeParse("far too long").Errors);

        Assert.Equal(ValidationErrorCode.TooBig, error.Code);
        Assert.Equal(ValidationOrigin.Text, error.Origin);

        // No message was given, so the provider chain wrote one from the facts.
        Assert.Equal("Must be at most 5 character(s) long.", error.Message);
    }

    [Fact]
    public void The_Rule_Runs_Only_After_Everything_Before_It_Passed()
    {
        var ran = false;

        var schema = Theo.String().MinLength(5).Refine((string _, ref ParseContext _) => ran = true);

        Assert.False(schema.SafeParse("ab").IsSuccess);
        Assert.False(ran);

        Assert.True(schema.SafeParse("abcdef").IsSuccess);
        Assert.True(ran);
    }

    [Fact]
    public void The_Rule_Reports_Through_The_Field_It_Is_Under()
    {
        var inner = Theo.String().Refine(static (string value, ref ParseContext context) =>
        {
            if (value.StartsWith('x'))
            {
                context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, "No x.");
            }
        });

        var schema = Theo.Object<CreateUserRequest>().Field(x => x.Name, inner);

        var result = schema.SafeParse(new CreateUserRequest { Name = "xavier" });

        Assert.Equal("Name", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public async Task The_Rule_Runs_On_The_Asynchronous_Path_Too()
    {
        var schema = Theo.String().Refine(static (string value, ref ParseContext context) =>
        {
            if (value == "no")
            {
                context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, "Not that.");
            }
        });

        Assert.True((await schema.SafeParseAsync("yes", cancellationToken: Ct)).IsSuccess);

        var result = await schema.SafeParseAsync("no", cancellationToken: Ct);
        Assert.Equal("Not that.", Assert.Single(result.Errors).Message);
    }

    // On the asynchronous path the rule is handed a synchronous context positioned where the parse has
    // reached, so a path it pushes still resolves against the value it is inside.
    [Fact]
    public async Task A_Path_Pushed_On_The_Asynchronous_Path_Is_Still_Correct()
    {
        var inner = Theo.String().Refine(static (string _, ref ParseContext context) =>
        {
            context.PushProperty("Inner");
            context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, "Nope.");
            context.Pop();
        });

        var schema = Theo.Object<CreateUserRequest>().Field(x => x.Name, inner);

        var result = await schema.SafeParseAsync(
            new CreateUserRequest { Name = "anything" },
            cancellationToken: Ct);

        Assert.Equal("Name.Inner", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Both_Forms_Of_Refine_Compose()
    {
        var schema = Theo.String()
            .Refine(static v => v.Length > 2, "Too short.")
            .Refine(static (string value, ref ParseContext context) =>
            {
                if (value.Contains(' ', StringComparison.Ordinal))
                {
                    context.AddError(new ValidationErrorInfo { Code = ValidationErrorCode.Custom }, "No spaces.");
                }
            });

        Assert.True(schema.IsValid("abc"));
        Assert.Equal("Too short.", Assert.Single(schema.SafeParse("ab").Errors).Message);
        Assert.Equal("No spaces.", Assert.Single(schema.SafeParse("a b c").Errors).Message);
    }

    [Fact]
    public void Refine_Refuses_Its_Nulls()
    {
        Assert.Throws<ArgumentNullException>(() => Theo.String().Refine((RefineRule<string>)null!));
        Assert.Throws<ArgumentNullException>(
            () => ContextualRefineExtensions.Refine<string>(null!, static (string _, ref ParseContext _) => { }));
    }
}
