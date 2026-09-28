using Theon.Errors;

namespace Theon.Tests;

public class ErrorMessageTests
{
    [Fact]
    public void A_Rule_Message_Beats_A_Call_Provider()
    {
        var options = new ParseOptions { MessageProvider = static (in ValidationErrorInfo _) => "from options" };

        var result = Theo.String().MinLength(5, "from the rule").SafeParse("a", options);

        Assert.Equal("from the rule", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_Call_Provider_Beats_The_Default()
    {
        var options = new ParseOptions { MessageProvider = static (in ValidationErrorInfo _) => "from options" };

        var result = Theo.String().MinLength(5).SafeParse("a", options);

        Assert.Equal("from options", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_Provider_Returning_Null_Falls_Through_To_The_Default()
    {
        var options = new ParseOptions
        {
            MessageProvider = static (in ValidationErrorInfo error) =>
                error.Code == ValidationErrorCode.InvalidFormat ? "handled" : null,
        };

        var tooShort = Theo.String().MinLength(5).SafeParse("a", options);
        var badEmail = Theo.String().Email().SafeParse("a", options);

        Assert.Equal("Must be at least 5 character(s) long.", Assert.Single(tooShort.Errors).Message);
        Assert.Equal("handled", Assert.Single(badEmail.Errors).Message);
    }

    [Fact]
    public void A_Provider_Can_Localize_From_The_Structured_Facts()
    {
        var options = new ParseOptions
        {
            MessageProvider = static (in ValidationErrorInfo error) => error switch
            {
                { Code: ValidationErrorCode.TooSmall, Origin: ValidationOrigin.Text } =>
                    $"Informe ao menos {error.Minimum} caractere(s).",
                { Code: ValidationErrorCode.InvalidFormat, Format: "email" } => "E-mail invalido.",
                _ => null,
            },
        };

        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3))
            .Field(x => x.Email, Theo.String().Email());

        var result = schema.SafeParse(new CreateUserRequest { Name = "a", Email = "b" }, options);

        Assert.Equal(
            new[] { "Informe ao menos 3 caractere(s).", "E-mail invalido." },
            result.Errors.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void ToString_Prefixes_The_Path()
    {
        var schema = Theo.Object<CreateUserRequest>().Field(x => x.Email, Theo.String().Email());

        var error = Assert.Single(schema.SafeParse(new CreateUserRequest()).Errors);

        Assert.Equal("Email: Invalid e-mail address.", error.ToString());
    }
}
