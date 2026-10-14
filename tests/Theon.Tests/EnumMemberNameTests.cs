using System.Text.Json;
using System.Text.Json.Serialization;

namespace Theon.Tests;

public enum Acao
{
    [JsonStringEnumMemberName("editar")]
    Editar,

    [JsonStringEnumMemberName("excluir")]
    Excluir,

    Visualizar,
}

/// <summary>
/// The names an enum goes out under.
/// </summary>
/// <remarks>
/// The document listed the C# member names — <c>Editar</c> — while the application serialized
/// <c>editar</c>, so the one list a client generated from the document had to match against was the
/// one list that was wrong.
/// </remarks>
public class EnumMemberNameTests
{
    private static string[] Allowed(JsonSchemaOptions? options = null) =>
        [.. Theo.Enum<Acao>().ToJsonSchema(options)["enum"]!
            .AsArray()
            .Select(static node => node!.GetValue<string>())];

    [Fact]
    public void A_Member_With_A_Serialized_Name_Goes_Out_Under_It()
    {
        Assert.Equal(["editar", "excluir", "Visualizar"], Allowed());
    }

    // An explicit name is explicit. This is how JsonStringEnumConverter behaves and the reason is the
    // same: somebody who wrote the name down meant that name and not a transformation of it.
    [Fact]
    public void A_Policy_Does_Not_Override_An_Explicit_Name()
    {
        var names = Allowed(new JsonSchemaOptions
        {
            EnumMemberNamingPolicy = JsonNamingPolicy.SnakeCaseUpper,
        });

        Assert.Equal(["editar", "excluir", "VISUALIZAR"], names);
    }

    [Fact]
    public void A_Policy_Names_The_Members_That_Did_Not_Say()
    {
        var names = Allowed(new JsonSchemaOptions
        {
            EnumMemberNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.Equal(["editar", "excluir", "visualizar"], names);
    }

    // Deliberately not the property policy. System.Text.Json configures the two independently --
    // properties through JsonSerializerOptions and enum members through JsonStringEnumConverter -- so
    // reading one off the other would put a wrong list into the document for anybody who set only the
    // first, and a wrong enum list becomes a wrong request body in a generated client.
    [Fact]
    public void The_Property_Policy_Does_Not_Name_Enum_Members()
    {
        var names = Allowed(new JsonSchemaOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        Assert.Equal(["editar", "excluir", "Visualizar"], names);
    }

    [Fact]
    public void An_Enum_With_No_Attributes_Is_Unchanged()
    {
        var names = Theo.Enum<TaskStatus>().ToJsonSchema()["enum"]!
            .AsArray()
            .Select(static node => node!.GetValue<string>());

        Assert.Equal(Enum.GetNames<TaskStatus>(), names);
    }

    // The parse side is unchanged and still takes a TEnum, which the type system has already settled.
    // Turning the serialized name back into a member is Theo.String().TryTransform, which exists.
    [Fact]
    public void The_Parse_Side_Still_Takes_The_Value()
    {
        Assert.True(Theo.Enum<Acao>().SafeParse(Acao.Editar).IsSuccess);
        Assert.False(Theo.Enum<Acao>().SafeParse((Acao)99).IsSuccess);
    }

    [Fact]
    public void A_Serialized_Name_Can_Be_Parsed_Back_With_TryTransform()
    {
        var schema = Theo.String().TryTransform<Acao>(
            static (string text, out Acao value) =>
                text switch
                {
                    "editar" => Set(Acao.Editar, out value),
                    "excluir" => Set(Acao.Excluir, out value),
                    "visualizar" => Set(Acao.Visualizar, out value),
                    _ => Set(default, out value) && false,
                },
            "Acao desconhecida.");

        Assert.Equal(Acao.Editar, schema.Parse("editar"));
        Assert.False(schema.SafeParse("Editar").IsSuccess);

        static bool Set(Acao from, out Acao value)
        {
            value = from;
            return true;
        }
    }

    [Fact]
    public void A_Renamed_Enum_Survives_The_Round_Trip()
    {
        var document = Theo.Enum<Acao>().ToJsonSchema(new JsonSchemaOptions
        {
            EnumMemberNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        var again = Theo.JsonSchema(document).ToJsonSchema();

        Assert.True(
            System.Text.Json.Nodes.JsonNode.DeepEquals(document, again),
            "out:  " + document.ToJsonString() + " back: " + again.ToJsonString());
    }
}
