using Theon.Errors;

namespace Theon.Localization.Tests;

public class BrazilianPortugueseMessagesTests
{
    private static readonly ParseOptions PtBr = new()
    {
        MessageProvider = BrazilianPortugueseMessages.Provider,
    };

    private static string Message<T>(Schema<T> schema, T value) =>
        schema.SafeParse(value, PtBr).Errors[0].Message;

    [Fact]
    public void Required_Field() =>
        Assert.Equal("Campo obrigatório.", Message(Theo.String(), null!));

    [Fact]
    public void Text_Length_Uses_The_Right_Plural()
    {
        Assert.Equal("Informe pelo menos 1 caractere.", Message(Theo.String().MinLength(1), ""));
        Assert.Equal("Informe pelo menos 3 caracteres.", Message(Theo.String().MinLength(3), "ab"));
        Assert.Equal("Informe no máximo 1 caractere.", Message(Theo.String().MaxLength(1), "abc"));
        Assert.Equal("Informe no máximo 5 caracteres.", Message(Theo.String().MaxLength(5), "abcdef"));
    }

    [Fact]
    public void Numeric_Bounds_Distinguish_Inclusive_From_Exclusive()
    {
        Assert.Equal("Deve ser maior ou igual a 18.", Message(Theo.Int().Min(18), 17));
        Assert.Equal("Deve ser maior que 0.", Message(Theo.Int().GreaterThan(0), 0));
        Assert.Equal("Deve ser menor ou igual a 120.", Message(Theo.Int().Max(120), 121));
        Assert.Equal("Deve ser menor que 10.", Message(Theo.Int().LessThan(10), 10));
    }

    [Fact]
    public void Collection_Counts_Use_The_Right_Plural()
    {
        var one = Theo.Collection(Theo.String()).MinCount(1);
        var three = Theo.Collection(Theo.String()).MinCount(3);

        Assert.Equal("Informe pelo menos 1 item.", Message(one, []));
        Assert.Equal("Informe pelo menos 3 itens.", Message(three, ["a"]));
    }

    [Fact]
    public void Formats() =>
        Assert.Equal("E-mail inválido.", Message(Theo.String().Email(), "nope"));

    [Fact]
    public void MultipleOf() =>
        Assert.Equal("Deve ser múltiplo de 5.", Message(Theo.Int().MultipleOf(5), 7));

    [Fact]
    public void A_Custom_Message_Is_Left_Alone()
    {
        // The provider declines a custom refinement: the caller already wrote that sentence, and
        // in whatever language they chose.
        var schema = Theo.String().Refine(_ => false, "Minha mensagem.");

        Assert.Equal("Minha mensagem.", Message(schema, "x"));
    }

    [Fact]
    public void Unhandled_Cases_Fall_Through_To_The_Default()
    {
        // Returning null rather than a wrong guess is what lets the chain keep working.
        var info = new ValidationErrorInfo { Code = ValidationErrorCode.Custom };

        Assert.Null(BrazilianPortugueseMessages.For(in info));
    }

    [Fact]
    public void A_Whole_Object_Reports_In_Portuguese()
    {
        var schema = Theo.Object<Request>()
            .Field(x => x.Nome, Theo.String().MinLength(3))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Idade, Theo.Int().Min(18));

        var result = schema.SafeParse(new Request { Nome = "A", Email = "x", Idade = 10 }, PtBr);

        Assert.Equal(
            new[] { "Informe pelo menos 3 caracteres.", "E-mail inválido.", "Deve ser maior ou igual a 18." },
            result.Errors.Select(e => e.Message).ToArray());
    }

    [Fact]
    public void Setting_It_Globally_Works()
    {
        var previous = SchemaGlobalOptions.MessageProvider;
        try
        {
            SchemaGlobalOptions.MessageProvider = BrazilianPortugueseMessages.Provider;

            Assert.Equal(
                "Informe pelo menos 3 caracteres.",
                Theo.String().MinLength(3).SafeParse("ab").Errors[0].Message);
        }
        finally
        {
            SchemaGlobalOptions.MessageProvider = previous;
        }
    }

    public sealed class Request
    {
        public string Nome { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int Idade { get; set; }
    }
}
