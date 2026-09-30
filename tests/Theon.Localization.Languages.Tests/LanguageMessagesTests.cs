using Theon.Errors;

namespace Theon.Localization.Tests;

/// <summary>
/// The three providers tested side by side, which is how a pluralization pasted from the wrong
/// language gets noticed.
/// </summary>
public class LanguageMessagesTests
{
    private static string Message<T>(Schema<T> schema, T value, SchemaErrorMessageProvider provider) =>
        schema.SafeParse(value, new ParseOptions { MessageProvider = provider }).Errors[0].Message;

    public static TheoryData<string, SchemaErrorMessageProvider> Providers() => new()
    {
        { "es", SpanishMessages.Provider },
        { "fr", FrenchMessages.Provider },
        { "de", GermanMessages.Provider },
        { "it", ItalianMessages.Provider },
        { "nl", DutchMessages.Provider },
        { "pl", PolishMessages.Provider },
    };

    [Fact]
    public void Required_Field()
    {
        Assert.Equal("Este campo es obligatorio.", Message(Theo.String(), null!, SpanishMessages.Provider));
        Assert.Equal("Ce champ est obligatoire.", Message(Theo.String(), null!, FrenchMessages.Provider));
        Assert.Equal("Dieses Feld ist erforderlich.", Message(Theo.String(), null!, GermanMessages.Provider));
    }

    [Fact]
    public void Spanish_Text_Length_Switches_Word_Not_Just_Suffix()
    {
        // "carácter" carries an accent the plural loses, so this cannot be done by appending an s.
        Assert.Equal(
            "Escriba al menos 1 carácter.",
            Message(Theo.String().MinLength(1), "", SpanishMessages.Provider));
        Assert.Equal(
            "Escriba al menos 3 caracteres.",
            Message(Theo.String().MinLength(3), "ab", SpanishMessages.Provider));
        Assert.Equal(
            "Escriba como máximo 1 carácter.",
            Message(Theo.String().MaxLength(1), "abc", SpanishMessages.Provider));
    }

    [Fact]
    public void French_Text_Length_Uses_The_Right_Plural()
    {
        Assert.Equal(
            "Saisissez au moins 1 caractère.",
            Message(Theo.String().MinLength(1), "", FrenchMessages.Provider));
        Assert.Equal(
            "Saisissez au moins 3 caractères.",
            Message(Theo.String().MinLength(3), "ab", FrenchMessages.Provider));
    }

    // German has one word for one character and for many, so the sentence does not change. Asserting
    // it is what stops someone "fixing" it into a plural that does not exist.
    [Fact]
    public void German_Text_Length_Has_No_Plural_To_Choose()
    {
        Assert.Equal(
            "Geben Sie mindestens 1 Zeichen ein.",
            Message(Theo.String().MinLength(1), "", GermanMessages.Provider));
        Assert.Equal(
            "Geben Sie mindestens 3 Zeichen ein.",
            Message(Theo.String().MinLength(3), "ab", GermanMessages.Provider));
    }

    [Fact]
    public void Collection_Counts_Use_The_Right_Plural()
    {
        var one = Theo.Collection(Theo.String()).MinCount(1);
        var three = Theo.Collection(Theo.String()).MinCount(3);

        Assert.Equal("Indique al menos 1 elemento.", Message(one, [], SpanishMessages.Provider));
        Assert.Equal("Indique al menos 3 elementos.", Message(three, ["a"], SpanishMessages.Provider));

        Assert.Equal("Indiquez au moins 1 élément.", Message(one, [], FrenchMessages.Provider));
        Assert.Equal("Indiquez au moins 3 éléments.", Message(three, ["a"], FrenchMessages.Provider));

        Assert.Equal("Geben Sie mindestens 1 Element an.", Message(one, [], GermanMessages.Provider));
        Assert.Equal("Geben Sie mindestens 3 Elemente an.", Message(three, ["a"], GermanMessages.Provider));
    }

    [Fact]
    public void Numeric_Bounds_Distinguish_Inclusive_From_Exclusive()
    {
        Assert.Equal(
            "Debe ser mayor o igual que 18.",
            Message(Theo.Int().Min(18), 17, SpanishMessages.Provider));
        Assert.Equal(
            "Debe ser mayor que 0.",
            Message(Theo.Int().GreaterThan(0), 0, SpanishMessages.Provider));

        Assert.Equal(
            "Doit être supérieur ou égal à 18.",
            Message(Theo.Int().Min(18), 17, FrenchMessages.Provider));
        Assert.Equal(
            "Doit être supérieur à 0.",
            Message(Theo.Int().GreaterThan(0), 0, FrenchMessages.Provider));

        Assert.Equal(
            "Muss größer oder gleich 18 sein.",
            Message(Theo.Int().Min(18), 17, GermanMessages.Provider));
        Assert.Equal(
            "Muss größer als 0 sein.",
            Message(Theo.Int().GreaterThan(0), 0, GermanMessages.Provider));
    }

    // A duration is not a point in time, so it gets the numeric sentence rather than the temporal
    // one. "Doit être postérieur à 00:00:05" would be wrong about a timeout in any language.
    [Fact]
    public void A_Duration_Reads_Like_A_Number()
    {
        var schema = Theo.TimeSpan().Min(TimeSpan.FromSeconds(5));
        var value = TimeSpan.FromSeconds(1);

        Assert.Equal("Debe ser mayor o igual que 00:00:05.", Message(schema, value, SpanishMessages.Provider));
        Assert.Equal("Doit être supérieur ou égal à 00:00:05.", Message(schema, value, FrenchMessages.Provider));
        Assert.Equal("Muss größer oder gleich 00:00:05 sein.", Message(schema, value, GermanMessages.Provider));
    }

    [Fact]
    public void Temporal_Bounds_Use_Temporal_Words()
    {
        var schema = Theo.DateOnly().Min(new DateOnly(2026, 1, 1));
        var value = new DateOnly(2025, 12, 31);

        Assert.StartsWith(
            "Debe ser igual o posterior a",
            Message(schema, value, SpanishMessages.Provider),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Doit être égal ou postérieur à",
            Message(schema, value, FrenchMessages.Provider),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Muss gleich oder nach",
            Message(schema, value, GermanMessages.Provider),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_Format_This_Library_Reports_Has_A_Sentence(
        string culture,
        SchemaErrorMessageProvider provider)
    {
        var cases = new (string Name, Schema<string> Schema, string Value)[]
        {
            ("email", Theo.String().Email(), "nope"),
            ("url", Theo.String().Url(), "nope"),
            ("uuid", Theo.String().Uuid(), "nope"),
            ("base64", Theo.String().Base64(), "QQ="),
            ("base64url", Theo.String().Base64Url(), "QQ=="),
            ("hex", Theo.String().Hex(), "zz"),
            ("e164", Theo.String().E164(), "nope"),
            ("iso8601", Theo.String().Iso8601(), "nope"),
            ("iso8601_date", Theo.String().Iso8601Date(), "nope"),
            ("ipv4", Theo.String().Ipv4(), "nope"),
            ("ipv6", Theo.String().Ipv6(), "nope"),
            ("cidr", Theo.String().Cidr(), "nope"),
            ("hostname", Theo.String().Hostname(), "-nope-"),
            ("jwt", Theo.String().Jwt(), "nope"),
            ("credit_card", Theo.String().CreditCard(), "nope"),
            ("iban", Theo.String().Iban(), "nope"),
            ("uppercase", Theo.String().Uppercase(), "abc"),
            ("lowercase", Theo.String().Lowercase(), "ABC"),
            ("starts_with", Theo.String().StartsWith("ab"), "zz"),
            ("ends_with", Theo.String().EndsWith("ab"), "zz"),
            ("contains", Theo.String().Contains("ab"), "zz"),
        };

        foreach (var (name, schema, value) in cases)
        {
            var error = schema.SafeParse(value).Errors[0];
            var message = provider(in error.Info);

            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"{culture} has no sentence for the {name} format, so it would fall back to English.");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Every_Error_Code_This_Library_Reports_Has_A_Sentence(
        string culture,
        SchemaErrorMessageProvider provider)
    {
        // Custom is deliberately absent: a refinement carries its own message, written by whoever
        // wrote the rule, and a provider has nothing to add to it.
        var codes = Enum.GetValues<ValidationErrorCode>()
            .Where(static code => code != ValidationErrorCode.Custom);

        foreach (var code in codes)
        {
            var info = new ValidationErrorInfo { Code = code, Expected = "x", Divisor = 2 };
            var message = provider(in info);

            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"{culture} has no sentence for {code}, so it would fall back to English.");
        }
    }

    [Fact]
    public void Italian_And_Dutch_Use_The_Right_Plural()
    {
        Assert.Equal(
            "Inserisci almeno 1 carattere.",
            Message(Theo.String().MinLength(1), "", ItalianMessages.Provider));
        Assert.Equal(
            "Inserisci almeno 3 caratteri.",
            Message(Theo.String().MinLength(3), "ab", ItalianMessages.Provider));

        Assert.Equal(
            "Voer minstens 1 teken in.",
            Message(Theo.String().MinLength(1), "", DutchMessages.Provider));
        Assert.Equal(
            "Voer minstens 3 tekens in.",
            Message(Theo.String().MinLength(3), "ab", DutchMessages.Provider));
    }

    // The reason this provider is worth having: Polish has three plural forms, and which one a number
    // takes depends on its last two digits rather than just the last one. Nothing in the core changed
    // to allow it, because a provider decides for itself how to reach a sentence.
    [Theory]
    [InlineData(1, "Wpisz co najmniej 1 znak.")]
    [InlineData(2, "Wpisz co najmniej 2 znaki.")]
    [InlineData(4, "Wpisz co najmniej 4 znaki.")]
    [InlineData(5, "Wpisz co najmniej 5 znaków.")]
    [InlineData(11, "Wpisz co najmniej 11 znaków.")]
    [InlineData(22, "Wpisz co najmniej 22 znaki.")]
    [InlineData(25, "Wpisz co najmniej 25 znaków.")]
    public void Polish_Chooses_Between_Three_Plural_Forms(int minimum, string expected) =>
        Assert.Equal(
            expected,
            Message(Theo.String().MinLength(minimum), string.Empty, PolishMessages.Provider));

    // Twelve to fourteen are the exception the last digit alone gets wrong: twelve takes the many form
    // while twenty-two takes the few form, though both end in a two.
    [Theory]
    [InlineData(12, "Wpisz co najmniej 12 znaków.")]
    [InlineData(13, "Wpisz co najmniej 13 znaków.")]
    [InlineData(14, "Wpisz co najmniej 14 znaków.")]
    public void Polish_Handles_The_Teens_That_Break_The_Rule(int minimum, string expected) =>
        Assert.Equal(
            expected,
            Message(Theo.String().MinLength(minimum), string.Empty, PolishMessages.Provider));

    [Fact]
    public void Polish_Names_The_Subject_So_The_Adjective_Agrees()
    {
        Assert.Equal(
            "Wartość musi być większa lub równa 18.",
            Message(Theo.Int().Min(18), 17, PolishMessages.Provider));

        Assert.StartsWith(
            "Data musi być równa lub późniejsza niż",
            Message(Theo.DateOnly().Min(new DateOnly(2026, 1, 1)), new DateOnly(2025, 1, 1), PolishMessages.Provider),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Required_Field_In_The_New_Languages()
    {
        Assert.Equal(
            "Questo campo è obbligatorio.",
            Message(Theo.String(), null!, ItalianMessages.Provider));
        Assert.Equal("Dit veld is verplicht.", Message(Theo.String(), null!, DutchMessages.Provider));
        Assert.Equal("To pole jest wymagane.", Message(Theo.String(), null!, PolishMessages.Provider));
    }

    [Fact]
    public void A_Refinements_Own_Message_Is_Never_Replaced()
    {
        var schema = Theo.String().Refine(static v => v.Length > 3, "Escríbalo completo.");

        Assert.Equal("Escríbalo completo.", Message(schema, "ab", SpanishMessages.Provider));
        Assert.Equal("Escríbalo completo.", Message(schema, "ab", FrenchMessages.Provider));
    }

    // The providers chain: one that declines a case lets the next answer. A provider is a function,
    // so composing two is composing two functions and needs nothing from the library.
    [Fact]
    public void A_Provider_That_Declines_Defers_To_The_Next()
    {
        static string? OnlyRequired(in ValidationErrorInfo error) =>
            error is { Code: ValidationErrorCode.InvalidType, Received: "null" } ? "¡Falta!" : null;

        var options = new ParseOptions
        {
            MessageProvider = (in ValidationErrorInfo error) =>
                OnlyRequired(in error) ?? SpanishMessages.For(in error),
        };

        Assert.Equal("¡Falta!", Theo.String().SafeParse(null!, options).Errors[0].Message);
        Assert.Equal(
            "Escriba al menos 3 caracteres.",
            Theo.String().MinLength(3).SafeParse("ab", options).Errors[0].Message);
    }
}
