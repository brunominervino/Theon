using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// An error code the caller chose, beside the one this library did.
/// </summary>
/// <remarks>
/// <para>
/// Codes are the contract and messages are not, which is the whole error model — and every refinement
/// produced the same code, <see cref="ValidationErrorCode.Custom"/>. A caller who needed to know
/// <em>which</em> refinement failed had to match on message text, which the error model explicitly
/// says is not a contract.
/// </para>
/// <para>
/// So there are two codes now: the enum, which is this library's vocabulary, and a string, which is
/// the caller's. The enum still says <c>Custom</c>, so nothing that switches on it changes.
/// </para>
/// </remarks>
public class CustomErrorCodeTests
{
    [Fact]
    public void A_Refinement_Can_Carry_A_Code_Of_The_Callers_Own()
    {
        var schema = Theo.String().Refine(
            static value => value.Length == 14,
            "Informe um CNPJ valido.",
            "cnpj.digito_verificador");

        var error = Assert.Single(schema.SafeParse("123").Errors);

        Assert.Equal("cnpj.digito_verificador", error.CustomCode);
        Assert.Equal("Informe um CNPJ valido.", error.Message);

        // The enum is unchanged, so a caller switching on it sees what it always saw.
        Assert.Equal(ValidationErrorCode.Custom, error.Code);
    }

    [Fact]
    public void An_Error_Without_One_Has_None()
    {
        var error = Assert.Single(Theo.String().MinLength(5).SafeParse("ab").Errors);

        Assert.Null(error.CustomCode);
        Assert.Null(error.Info.CustomCode);
    }

    // A rule that reports for itself reaches the whole of ValidationErrorInfo, so it reaches this too
    // without anything being added for it.
    [Fact]
    public void A_Rule_That_Reports_For_Itself_Can_Set_One()
    {
        var schema = Theo.Object<SignUp>().Refine((SignUp value, ref ParseContext context) =>
        {
            if (value.Password != value.PasswordConfirmation)
            {
                context.PushProperty(nameof(SignUp.PasswordConfirmation));
                context.AddError(
                    new ValidationErrorInfo
                    {
                        Code = ValidationErrorCode.Custom,
                        CustomCode = "senha.confirmacao_divergente",
                    },
                    "As senhas nao conferem.");
                context.Pop();
            }
        });

        var value = new SignUp { Password = "abcdefgh", PasswordConfirmation = "outra" };
        var error = Assert.Single(schema.SafeParse(value).Errors);

        Assert.Equal("senha.confirmacao_divergente", error.CustomCode);
        Assert.Equal("PasswordConfirmation", error.Path.ToString());
    }

    // Leaving the message out is what hands the sentence to the provider, and the code is what the
    // provider recognises. This is the localization case: the project's own provider writes the
    // sentence for one particular rule.
    [Fact]
    public void A_Rule_With_No_Message_Lets_The_Provider_Answer_From_The_Code()
    {
        var seen = new List<string?>();

        var schema = Theo.String().Refine(
            static value => false,
            message: null,
            code: "cnpj.digito_verificador");

        var result = schema.SafeParse("x", new ParseOptions
        {
            MessageProvider = (in ValidationErrorInfo error) =>
            {
                seen.Add(error.CustomCode);
                return error.CustomCode == "cnpj.digito_verificador"
                    ? "Os digitos verificadores do CNPJ nao conferem."
                    : null;
            },
        });

        Assert.Equal(["cnpj.digito_verificador"], seen);
        Assert.Equal(
            "Os digitos verificadores do CNPJ nao conferem.",
            Assert.Single(result.Errors).Message);
    }

    // The order decision 5 settled, unchanged: a message on the rule beats every provider, so a
    // provider is never even asked. Worth a test of its own, because the obvious reading of "the code
    // reaches the provider" is that the provider gets to override -- and it does not.
    [Fact]
    public void A_Message_On_The_Rule_Still_Wins_Over_The_Provider()
    {
        var asked = false;

        var schema = Theo.String().Refine(
            static value => false,
            "Informe um CNPJ valido.",
            "cnpj.digito_verificador");

        var result = schema.SafeParse("x", new ParseOptions
        {
            MessageProvider = (in ValidationErrorInfo error) =>
            {
                asked = true;
                return "o provedor falou";
            },
        });

        Assert.Equal("Informe um CNPJ valido.", Assert.Single(result.Errors).Message);
        Assert.False(asked);
    }

    // No provider and no message is a code nobody has written a sentence for yet. It still carries the
    // code, so a caller branching on codes is served even where nobody translated it.
    [Fact]
    public void A_Code_Nobody_Wrote_A_Sentence_For_Still_Carries_The_Code()
    {
        var error = Assert.Single(Theo.String()
            .Refine(static value => false, message: null, code: "regra.sem_frase")
            .SafeParse("x").Errors);

        Assert.Equal("regra.sem_frase", error.CustomCode);
        Assert.Equal("Invalid input.", error.Message);
    }

    [Fact]
    public void A_Passing_Refinement_Reports_Nothing() =>
        Assert.True(Theo.String()
            .Refine(static value => true, "never", "never.happens")
            .SafeParse("x").IsSuccess);

    [Fact]
    public void The_Predicate_And_The_Code_Are_Required()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Theo.String().Refine(null!, "msg", "code"));

        Assert.ThrowsAny<ArgumentException>(() =>
            Theo.String().Refine(static _ => true, "msg", null!));

        Assert.ThrowsAny<ArgumentException>(() =>
            Theo.String().Refine(static _ => true, "msg", string.Empty));
    }
}
