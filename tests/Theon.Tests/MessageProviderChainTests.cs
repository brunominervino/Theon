using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// Two message providers, in an order.
/// </summary>
/// <remarks>
/// A provider returning <see langword="null"/> declines, which is what lets one localize the cases it
/// cares about and ignore the rest. That was already true of one provider falling through to the
/// built-in English; what was missing was any way to put two of them in that order — an application
/// naming its own rules, with a language package behind it saying everything else.
/// </remarks>
public class MessageProviderChainTests
{
    private static SchemaErrorMessageProvider Answers(string? message) =>
        (in ValidationErrorInfo error) => message;

    private static string Message<T>(Schema<T> schema, T value, SchemaErrorMessageProvider provider) =>
        schema.SafeParse(value, new ParseOptions { MessageProvider = provider }).Errors[0].Message;

    [Fact]
    public void The_First_Provider_To_Answer_Wins()
    {
        var chain = MessageProviders.Chain(Answers("primeiro"), Answers("segundo"));

        Assert.Equal("primeiro", Message(Theo.String().MinLength(5), "ab", chain));
    }

    [Fact]
    public void One_That_Declines_Hands_On_To_The_Next()
    {
        var chain = MessageProviders.Chain(Answers(null), Answers("segundo"));

        Assert.Equal("segundo", Message(Theo.String().MinLength(5), "ab", chain));
    }

    // All of them declining is not the chain's problem: the error falls through to the provider on
    // SchemaGlobalOptions and then to the built-in English, exactly as one declining provider does.
    [Fact]
    public void All_Of_Them_Declining_Falls_Through_To_The_Defaults()
    {
        var chain = MessageProviders.Chain(Answers(null), Answers(null));

        Assert.Equal(
            "Must be at least 5 character(s) long.",
            Message(Theo.String().MinLength(5), "ab", chain));
    }

    [Fact]
    public void Each_Provider_Sees_The_Facts()
    {
        var seen = new List<ValidationErrorCode>();

        var chain = MessageProviders.Chain(
            (in ValidationErrorInfo error) =>
            {
                seen.Add(error.Code);
                return null;
            },
            (in ValidationErrorInfo error) =>
            {
                seen.Add(error.Code);
                return "segundo";
            });

        Message(Theo.String().MinLength(5), "ab", chain);

        Assert.Equal([ValidationErrorCode.TooSmall, ValidationErrorCode.TooSmall], seen);
    }

    // The documented shape: the application names its own rules and the language package says the
    // rest. The code from the application's rule is what its own provider recognises.
    [Fact]
    public void An_Application_Provider_In_Front_Of_A_Language_Package()
    {
        SchemaErrorMessageProvider application = (in ValidationErrorInfo error) =>
            error.CustomCode == "cnpj.digito_verificador"
                ? "Os digitos verificadores do CNPJ nao conferem."
                : null;

        var chain = MessageProviders.Chain(application, Answers("a lingua disse"));

        var mine = Theo.String().Refine(static _ => false, message: null, code: "cnpj.digito_verificador");
        var other = Theo.String().MinLength(5);

        Assert.Equal("Os digitos verificadores do CNPJ nao conferem.", Message(mine, "x", chain));
        Assert.Equal("a lingua disse", Message(other, "ab", chain));
    }

    // A chain of one is that one provider, with nothing between the caller and it.
    [Fact]
    public void A_Chain_Of_One_Is_That_Provider()
    {
        var only = Answers("so eu");

        Assert.Same(only, MessageProviders.Chain(only));
    }

    [Fact]
    public void An_Array_The_Caller_Changes_Afterwards_Does_Not_Change_The_Chain()
    {
        var providers = new[] { Answers(null), Answers("segundo") };
        var chain = MessageProviders.Chain(providers);

        providers[1] = Answers("trocado");

        Assert.Equal("segundo", Message(Theo.String().MinLength(5), "ab", chain));
    }

    [Fact]
    public void A_Chain_Of_None_Is_Refused() =>
        Assert.Throws<ArgumentException>(() => MessageProviders.Chain());

    // Refused where the chain is built rather than on the first error, which is the only place a
    // person can act on it.
    [Fact]
    public void A_Hole_In_The_Chain_Is_Refused_At_The_Line_That_Built_It() =>
        Assert.Throws<ArgumentException>(() => MessageProviders.Chain(Answers("x"), null!));

    [Fact]
    public void A_Null_Array_Is_Refused() =>
        Assert.Throws<ArgumentNullException>(() => MessageProviders.Chain(null!));
}
