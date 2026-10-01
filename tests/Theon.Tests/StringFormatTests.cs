using Theon.Errors;

namespace Theon.Tests;

public class StringFormatTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@example.co.uk")]
    [InlineData("user+tag@example.com")]
    [InlineData("u@e.io")]
    [InlineData("a_b-c@sub.domain.example.com")]
    public void Email_Accepts_Ordinary_Addresses(string value) =>
        Assert.True(Theo.String().Email().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("plainstring")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@example")]
    [InlineData("user@@example.com")]
    [InlineData("user name@example.com")]
    [InlineData(".user@example.com")]
    [InlineData("user.@example.com")]
    [InlineData("user@.example.com")]
    [InlineData("user@example..com")]
    public void Email_Rejects_Malformed_Addresses(string value) =>
        Assert.False(Theo.String().Email().IsValid(value));

    [Fact]
    public void Email_Reports_The_Format_Name()
    {
        var result = Theo.String().Email().SafeParse("nope");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidFormat, error.Code);
        Assert.Equal("email", error.Format);
        Assert.Equal("Invalid e-mail address.", error.Message);
    }

    [Fact]
    public void Email_Is_Not_Fooled_By_A_Newline()
    {
        // An unanchored pattern would match the first line and let the rest through.
        Assert.False(Theo.String().Email().IsValid("user@example.com\nnot-an-email"));
    }

    [Fact]
    public void StartsWith_And_EndsWith_And_Contains()
    {
        Assert.True(Theo.String().StartsWith("ab").IsValid("abc"));
        Assert.False(Theo.String().StartsWith("ab").IsValid("xabc"));
        Assert.True(Theo.String().EndsWith("bc").IsValid("abc"));
        Assert.False(Theo.String().EndsWith("bc").IsValid("abcx"));
        Assert.True(Theo.String().Contains("b").IsValid("abc"));
        Assert.False(Theo.String().Contains("z").IsValid("abc"));
    }

    [Fact]
    public void Substring_Rules_Are_Ordinal_By_Default()
    {
        Assert.False(Theo.String().StartsWith("AB").IsValid("abc"));
        Assert.True(Theo.String().StartsWith("AB", StringComparison.OrdinalIgnoreCase).IsValid("abc"));
    }

    [Fact]
    public void Lowercase_And_Uppercase()
    {
        Assert.True(Theo.String().Lowercase().IsValid("abc123"));
        Assert.False(Theo.String().Lowercase().IsValid("abC"));
        Assert.True(Theo.String().Uppercase().IsValid("ABC123"));
        Assert.False(Theo.String().Uppercase().IsValid("ABc"));
    }

    [Fact]
    public void Refine_Reports_Its_Own_Message()
    {
        var schema = Theo.String().Refine(static v => v.Length % 2 == 0, "Must have an even length.");

        var result = schema.SafeParse("abc");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.Custom, error.Code);
        Assert.Equal("Must have an even length.", error.Message);
    }

    [Fact]
    public void A_Rule_Message_Overrides_The_Default()
    {
        var result = Theo.String().MinLength(5, "Too short, sorry.").SafeParse("ab");

        Assert.Equal("Too short, sorry.", Assert.Single(result.Errors).Message);
    }

    // Every format pattern is anchored \A..\z rather than ^..$, because in .NET $ also matches
    // immediately before one trailing newline. With ^..$ these all passed, which is how a line
    // break survives the one rule whose job is to reject it.
    [Theory]
    [InlineData("user@example.com\n")]
    [InlineData("https://example.com\n")]
    [InlineData("6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f6\n")]
    [InlineData("+5511987654321\n")]
    public void A_Trailing_Newline_Does_Not_Slip_Past_An_Anchor(string value)
    {
        Assert.False(Theo.String().Email().IsValid(value));
        Assert.False(Theo.String().Url().IsValid(value));
        Assert.False(Theo.String().Uuid().IsValid(value));
        Assert.False(Theo.String().E164().IsValid(value));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/")]
    [InlineData("https://www.example.co.uk/a/b?q=1&r=2#frag")]
    [InlineData("https://example.com:8443/x")]
    [InlineData("https://sub.domain.example.com")]
    [InlineData("https://192.168.0.1/admin")]
    [InlineData("https://example.com/a%20b")]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:5000")]
    public void Url_Accepts_Ordinary_Addresses(string value) =>
        Assert.True(Theo.String().Url().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("https://")]
    [InlineData("//example.com")]
    [InlineData("http://exa mple.com")]
    [InlineData("http://-bad.com")]
    [InlineData("https://exa_mple.com")]
    [InlineData("https://example.com:123456/x")]
    public void Url_Rejects_Malformed_Addresses(string value) =>
        Assert.False(Theo.String().Url().IsValid(value));

    // Not oversights: a scheme that executes, and credentials nobody meant to type. Both are legal
    // URLs and neither belongs in a field labelled "website".
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user:pass@example.com")]
    public void Url_Refuses_What_A_Specification_Would_Allow(string value) =>
        Assert.False(Theo.String().Url().IsValid(value));

    [Fact]
    public void Url_Reports_The_Format_Name()
    {
        var result = Theo.String().Url().SafeParse("nope");

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidFormat, error.Code);
        Assert.Equal("url", error.Format);
        Assert.Equal("Invalid web address.", error.Message);
    }

    [Theory]
    [InlineData("6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f6")]
    [InlineData("6F0D6E0A-1B2C-4D5E-8F90-A1B2C3D4E5F6")]
    [InlineData("6f0d6E0a-1B2c-4d5E-8f90-A1b2C3d4E5f6")]
    public void Uuid_Accepts_The_Canonical_Form(string value) =>
        Assert.True(Theo.String().Uuid().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("6f0d6e0a1b2c4d5e8f90a1b2c3d4e5f6")]
    [InlineData("{6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f6}")]
    [InlineData("6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f")]
    [InlineData("6f0d6e0a-1b2c-4d5e-8f90-a1b2c3d4e5f6g")]
    [InlineData("6f0d6e0a_1b2c_4d5e_8f90_a1b2c3d4e5f6")]
    [InlineData("6f0d6e0a-1b2c-4d5e-8f90a1b2c3d4e5f6")]
    public void Uuid_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Uuid().IsValid(value));

    // The version and variant digits are not constrained, so Guid.Empty round-trips and a UUIDv7
    // is not rejected for being newer than the rule. A mistyped identifier comes out the wrong
    // length, not the wrong version.
    [Fact]
    public void Uuid_Does_Not_Police_The_Version()
    {
        Assert.True(Theo.String().Uuid().IsValid(System.Guid.Empty.ToString()));
        Assert.True(Theo.String().Uuid().IsValid("0195f4a8-7b3c-7000-8000-0123456789ab"));
        Assert.True(Theo.String().Uuid().IsValid("ffffffff-ffff-ffff-ffff-ffffffffffff"));
    }

    [Theory]
    [InlineData("QQ==")]
    [InlineData("QUJD")]
    [InlineData("QUJDRA==")]
    [InlineData("QUJDRUY=")]
    [InlineData("a+b/c8==")]
    public void Base64_Accepts_Padded_Standard_Alphabet(string value) =>
        Assert.True(Theo.String().Base64().IsValid(value));

    [Theory]
    [InlineData("QQ=")]
    [InlineData("QQ===")]
    [InlineData("A")]
    [InlineData("****")]
    [InlineData("QUJ-")]
    [InlineData("=QQ=")]
    [InlineData("QQ== ")]
    public void Base64_Rejects_Bad_Padding_And_Foreign_Characters(string value) =>
        Assert.False(Theo.String().Base64().IsValid(value));

    // The empty string is the encoding of the empty byte array and round-trips as one, so it is
    // valid base64. Requiring a value is a separate rule, and says so.
    [Fact]
    public void Base64_Accepts_The_Empty_String_And_NotEmpty_Composes()
    {
        Assert.True(Theo.String().Base64().IsValid(""));
        Assert.False(Theo.String().Base64().NotEmpty().IsValid(""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("QQ")]
    [InlineData("QUJ")]
    [InlineData("QUJD")]
    [InlineData("-_-_")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9")]
    public void Base64Url_Accepts_The_UrlSafe_Alphabet(string value) =>
        Assert.True(Theo.String().Base64Url().IsValid(value));

    // Padding is refused rather than tolerated: a JSON web token, a URL segment and a filename all
    // omit it, so a padded value came from the wrong encoder.
    [Theory]
    [InlineData("Q")]
    [InlineData("QQ==")]
    [InlineData("QU+D")]
    [InlineData("QU/D")]
    [InlineData("QUJDQ")]
    public void Base64Url_Rejects_Padding_And_The_Standard_Alphabet(string value) =>
        Assert.False(Theo.String().Base64Url().IsValid(value));

    [Theory]
    [InlineData("0")]
    [InlineData("deadBEEF")]
    [InlineData("0123456789abcdefABCDEF")]
    public void Hex_Accepts_Hexadecimal_Digits(string value) =>
        Assert.True(Theo.String().Hex().IsValid(value));

    // Unlike base64, the empty string is rejected: no encoder emits it for anything, so it is
    // always an absent value wearing the wrong error.
    [Theory]
    [InlineData("")]
    [InlineData("0x1f")]
    [InlineData("gg")]
    [InlineData("de ad")]
    public void Hex_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Hex().IsValid(value));

    [Fact]
    public void Hex_Composes_With_Length_To_Pin_A_Digest()
    {
        var sha256 = Theo.String().Hex().Length(64);

        Assert.True(sha256.IsValid(new string('a', 64)));
        Assert.False(sha256.IsValid(new string('a', 63)));
    }

    [Theory]
    [InlineData("+5511987654321")]
    [InlineData("+12125551234")]
    [InlineData("+441632960961")]
    [InlineData("+551198765432109")]
    public void E164_Accepts_Interchange_Form(string value) =>
        Assert.True(Theo.String().E164().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("5511987654321")]
    [InlineData("+05511987654321")]
    [InlineData("+1")]
    [InlineData("+5511987654321098")]
    [InlineData("+55 11 98765-4321")]
    [InlineData("(11) 98765-4321")]
    [InlineData("++5511987654321")]
    public void E164_Rejects_Presentation_Form(string value) =>
        Assert.False(Theo.String().E164().IsValid(value));

    [Fact]
    public void E164_Reports_A_Message_A_Person_Can_Read()
    {
        var result = Theo.String().E164().SafeParse("(11) 98765-4321");

        var error = Assert.Single(result.Errors);
        Assert.Equal("e164", error.Format);
        Assert.Equal("Invalid phone number.", error.Message);
    }

    [Theory]
    [InlineData("2026-09-30T14:30:00Z")]
    [InlineData("2026-09-30T14:30:00")]
    [InlineData("2026-09-30T14:30")]
    [InlineData("2026-09-30T14:30:00.123Z")]
    [InlineData("2026-09-30T14:30:00.1234567+03:00")]
    [InlineData("2026-09-30T14:30:00-05:00")]
    [InlineData("2026-09-30T14:30:00+0300")]
    public void Iso8601_Accepts_The_Forms_Dotnet_Writes(string value) =>
        Assert.True(Theo.String().Iso8601().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("2026-09-30")]
    [InlineData("09/30/2026")]
    [InlineData("2026-09-30T25:30:00Z")]
    [InlineData("2026-13-01T14:30:00Z")]
    [InlineData("2026-09-30T14:30:00 ")]
    public void Iso8601_Rejects_Other_Shapes(string value) =>
        Assert.False(Theo.String().Iso8601().IsValid(value));

    // A space where the T belongs is a different serialization, not a typo, so it is refused
    // rather than quietly accepted.
    [Fact]
    public void Iso8601_Refuses_A_Space_Separator() =>
        Assert.False(Theo.String().Iso8601().IsValid("2026-09-30 14:30:00"));

    // The edge case that decided the implementation. A regular expression can describe the shape
    // of a date and cannot tell February from the number 31, so this rule parses instead.
    [Theory]
    [InlineData("2026-02-31")]
    [InlineData("2026-04-31")]
    [InlineData("2027-02-29")]
    [InlineData("2026-06-00")]
    public void Iso8601Date_Checks_The_Calendar_Not_Just_The_Shape(string value) =>
        Assert.False(Theo.String().Iso8601Date().IsValid(value));

    [Theory]
    [InlineData("2026-09-30")]
    [InlineData("2028-02-29")]
    [InlineData("0001-01-01")]
    public void Iso8601Date_Accepts_Dates_That_Exist(string value) =>
        Assert.True(Theo.String().Iso8601Date().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("2026-9-30")]
    [InlineData("26-09-30")]
    [InlineData("2026/09/30")]
    [InlineData("2026-09-30T00:00:00Z")]
    public void Iso8601Date_Rejects_Other_Shapes(string value) =>
        Assert.False(Theo.String().Iso8601Date().IsValid(value));

    [Theory]
    [InlineData("14:30:00")]
    [InlineData("00:00:00")]
    [InlineData("23:59:59")]
    [InlineData("14:30")]
    [InlineData("14:30:00.123")]
    [InlineData("14:30:00.1234567")]
    public void Iso8601Time_Accepts_Times_Of_Day(string value) =>
        Assert.True(Theo.String().Iso8601Time().IsValid(value));

    // The clock is checked, not just the shape.
    [Theory]
    [InlineData("24:00:00")]
    [InlineData("14:60:00")]
    [InlineData("14:30:60")]
    public void Iso8601Time_Checks_The_Clock(string value) =>
        Assert.False(Theo.String().Iso8601Time().IsValid(value));

    // No offset: a TimeOnly has nowhere to put one, and a time of day that carries an offset is a
    // DateTimeOffset rather than a time.
    [Theory]
    [InlineData("")]
    [InlineData("2:30:00")]
    [InlineData("14:30:00Z")]
    [InlineData("14:30:00+01:00")]
    [InlineData("14-30-00")]
    public void Iso8601Time_Rejects_Other_Shapes(string value) =>
        Assert.False(Theo.String().Iso8601Time().IsValid(value));

    [Theory]
    [InlineData("P1D")]
    [InlineData("PT5S")]
    [InlineData("P1Y2M3DT4H5M6S")]
    [InlineData("PT1H30M")]
    [InlineData("P1DT1H")]
    [InlineData("P10Y")]
    [InlineData("P1M")]
    [InlineData("PT1M")]
    [InlineData("P3W")]
    public void Iso8601Duration_Accepts_Durations(string value) =>
        Assert.True(Theo.String().Iso8601Duration().IsValid(value));

    // A fraction on any component rather than only the last, because producers vary and PT0.5H is not
    // a typo.
    [Theory]
    [InlineData("PT0.5S")]
    [InlineData("PT1,5S")]
    [InlineData("PT0.5H")]
    public void Iso8601Duration_Accepts_A_Fraction(string value) =>
        Assert.True(Theo.String().Iso8601Duration().IsValid(value));

    // The ordering and the one-of-each rule are what a looser check would miss: P and M mean months
    // before the T and minutes after it, so the same letter is two different units.
    [Theory]
    [InlineData("PT1D")]
    [InlineData("P1H")]
    [InlineData("P1S")]
    [InlineData("P1M2Y")]
    [InlineData("PT1S1S")]
    public void Iso8601Duration_Enforces_The_Order_And_One_Of_Each(string value) =>
        Assert.False(Theo.String().Iso8601Duration().IsValid(value));

    // A count of weeks cannot be combined with anything else.
    [Fact]
    public void Iso8601Duration_Keeps_The_Week_Form_On_Its_Own()
    {
        Assert.True(Theo.String().Iso8601Duration().IsValid("P3W"));
        Assert.False(Theo.String().Iso8601Duration().IsValid("P3W1D"));
        Assert.False(Theo.String().Iso8601Duration().IsValid("P1DT3W"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("P")]
    [InlineData("PT")]
    [InlineData("1D")]
    [InlineData("P1X")]
    [InlineData("P1DT")]
    [InlineData("PS")]
    [InlineData("P1.D")]
    [InlineData("PT.5S")]
    public void Iso8601Duration_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Iso8601Duration().IsValid(value));

    // A negative duration in a configuration value is a mistake rather than an intention, so the sign
    // the specification permits is refused.
    [Fact]
    public void Iso8601Duration_Refuses_A_Leading_Sign() =>
        Assert.False(Theo.String().Iso8601Duration().IsValid("-P1D"));

    // This is not the form .NET writes for a TimeSpan, and the two rules do not overlap.
    [Fact]
    public void Iso8601Duration_And_TimeSpan_Describe_Different_Things()
    {
        Assert.False(Theo.String().Iso8601Duration().IsValid("00:00:05"));
        Assert.True(Theo.TimeSpan().IsValid(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Iso8601_Reports_Its_Format_Names()
    {
        var dateTime = Theo.String().Iso8601().SafeParse("nope");
        var date = Theo.String().Iso8601Date().SafeParse("nope");

        Assert.Equal("iso8601", Assert.Single(dateTime.Errors).Format);
        Assert.Equal("Invalid date and time.", Assert.Single(dateTime.Errors).Message);
        Assert.Equal("iso8601_date", Assert.Single(date.Errors).Format);
        Assert.Equal("Invalid date.", Assert.Single(date.Errors).Message);
    }
}
