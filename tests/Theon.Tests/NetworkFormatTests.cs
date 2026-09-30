namespace Theon.Tests;

/// <summary>
/// Addresses, host names, tokens, and the two formats that are checksums rather than shapes.
/// </summary>
public class NetworkFormatTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("10.0.0.7")]
    [InlineData("127.0.0.1")]
    public void Ipv4_Accepts_Dotted_Decimal(string value) =>
        Assert.True(Theo.String().Ipv4().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("256.0.0.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.2.3.")]
    [InlineData(".1.2.3")]
    [InlineData("1..2.3")]
    [InlineData("1.2.3.4 ")]
    [InlineData("1.2.3.4:80")]
    [InlineData("::1")]
    public void Ipv4_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Ipv4().IsValid(value));

    // A leading zero is refused because some resolvers read it as octal and others as decimal, so
    // 010.1.1.1 is two different addresses depending on who is looking.
    [Theory]
    [InlineData("01.2.3.4")]
    [InlineData("1.2.3.04")]
    [InlineData("010.1.1.1")]
    public void Ipv4_Refuses_A_Leading_Zero(string value) =>
        Assert.False(Theo.String().Ipv4().IsValid(value));

    [Theory]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("fe80::1")]
    [InlineData("1:2:3:4:5:6:7:8")]
    [InlineData("1::8")]
    [InlineData("2001:db8::")]
    [InlineData("2001:db8:0:0:1::1")]
    public void Ipv6_Accepts_Full_And_Compressed_Forms(string value) =>
        Assert.True(Theo.String().Ipv6().IsValid(value));

    [Theory]
    [InlineData("::ffff:192.168.0.1")]
    [InlineData("64:ff9b::1.2.3.4")]
    [InlineData("::ffff:0:255.255.255.255")]
    public void Ipv6_Accepts_An_Embedded_Ipv4_Tail(string value) =>
        Assert.True(Theo.String().Ipv6().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("2001:db8")]
    [InlineData("gggg::1")]
    [InlineData("1.2.3.4")]
    [InlineData("12345::1")]
    [InlineData(":1")]
    [InlineData("1:")]
    [InlineData("1:2:3:4:5:6:7:8:9")]
    [InlineData("1:2:3:4:5:6:7:8::")]
    [InlineData("::1.2.3")]
    [InlineData("2001:db8::1 ")]
    public void Ipv6_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Ipv6().IsValid(value));

    // Two runs of colons would leave the number of omitted groups ambiguous, so only one is allowed.
    [Theory]
    [InlineData("2001:db8::1::2")]
    [InlineData("2001:db8:::1")]
    public void Ipv6_Allows_Only_One_Run_Of_Colons(string value) =>
        Assert.False(Theo.String().Ipv6().IsValid(value));

    [Theory]
    [InlineData("10.0.0.0/8")]
    [InlineData("192.168.1.0/24")]
    [InlineData("0.0.0.0/0")]
    [InlineData("1.2.3.4/32")]
    [InlineData("2001:db8::/32")]
    [InlineData("::/0")]
    [InlineData("fe80::/128")]
    public void Cidr_Accepts_Either_Family(string value) =>
        Assert.True(Theo.String().Cidr().IsValid(value));

    // The prefix length is checked against the family the address belongs to, which is the part a
    // single pattern over both families would have had to restate.
    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/64")]
    [InlineData("2001:db8::/129")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/")]
    [InlineData("/8")]
    [InlineData("10.0.0.0/08")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("")]
    public void Cidr_Rejects_A_Prefix_Its_Family_Cannot_Have(string value) =>
        Assert.False(Theo.String().Cidr().IsValid(value));

    [Theory]
    [InlineData("localhost")]
    [InlineData("example.com")]
    [InlineData("a")]
    [InlineData("sub.domain.example.co.uk")]
    [InlineData("my-host")]
    [InlineData("xn--bcher-kva.example")]
    public void Hostname_Accepts_Ordinary_Names(string value) =>
        Assert.True(Theo.String().Hostname().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("-bad.com")]
    [InlineData("bad-.com")]
    [InlineData("exa mple.com")]
    [InlineData("example..com")]
    [InlineData(".example.com")]
    [InlineData("example.com.")]
    [InlineData("under_score.com")]
    public void Hostname_Rejects_Malformed_Names(string value) =>
        Assert.False(Theo.String().Hostname().IsValid(value));

    // A single label passes here and fails inside Url, and that is the difference between a
    // configuration value and a web address a person typed.
    [Fact]
    public void A_Single_Label_Is_A_Hostname_But_Not_A_Url()
    {
        Assert.True(Theo.String().Hostname().IsValid("intranet"));
        Assert.False(Theo.String().Url().IsValid("https://intranet"));
    }

    [Fact]
    public void Jwt_Accepts_Three_Segments() =>
        Assert.True(Theo.String().Jwt().IsValid(
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Theory]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData(".b.c")]
    [InlineData("a.b.c=")]
    [InlineData("a.b+c.d")]
    public void Jwt_Rejects_Anything_Else(string value) =>
        Assert.False(Theo.String().Jwt().IsValid(value));

    // An unsecured token -- algorithm "none", empty signature -- is legal and is a way in. Requiring
    // the third segment refuses it, which is the same judgement Url makes about the javascript scheme.
    [Fact]
    public void Jwt_Refuses_An_Empty_Signature() =>
        Assert.False(Theo.String().Jwt().IsValid("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0."));

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("5555555555554444")]
    [InlineData("378282246310005")]
    [InlineData("6011111111111117")]
    [InlineData("30569309025904")]
    [InlineData("4012888888881881")]
    public void CreditCard_Accepts_Numbers_Whose_Checksum_Agrees(string value) =>
        Assert.True(Theo.String().CreditCard().IsValid(value));

    // The whole reason this is a checksum and not a pattern: a pattern accepts every one of these.
    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("4111111111111121")]
    [InlineData("1234567890123456")]
    public void CreditCard_Catches_A_Mistyped_Or_Transposed_Digit(string value) =>
        Assert.False(Theo.String().CreditCard().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("411111111111")]
    [InlineData("41111111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("abcdefghijklmnop")]
    public void CreditCard_Rejects_Presentation_Form_And_Wrong_Lengths(string value) =>
        Assert.False(Theo.String().CreditCard().IsValid(value));

    [Theory]
    [InlineData("GB82WEST12345698765432")]
    [InlineData("DE89370400440532013000")]
    [InlineData("FR1420041010050500013M02606")]
    [InlineData("BR9700360305000010009795493P1")]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("CH9300762011623852957")]
    [InlineData("PT50000201231234567890154")]
    public void Iban_Accepts_Numbers_Whose_Checksum_Agrees(string value) =>
        Assert.True(Theo.String().Iban().IsValid(value));

    [Theory]
    [InlineData("")]
    [InlineData("GB82WEST12345698765431")]
    [InlineData("G182WEST12345698765432")]
    [InlineData("GBX2WEST12345698765432")]
    [InlineData("GB82")]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    public void Iban_Rejects_A_Failed_Checksum_Or_A_Wrong_Shape(string value) =>
        Assert.False(Theo.String().Iban().IsValid(value));

    // Uppercase is the interchange form, and normalizing first is the idiom rather than a hidden
    // comparison.
    [Fact]
    public void Iban_Is_Uppercase_And_Normalization_Composes()
    {
        Assert.False(Theo.String().Iban().IsValid("gb82west12345698765432"));
        Assert.True(Theo.String().ToUpperInvariant().Iban().IsValid("gb82west12345698765432"));
    }

    [Theory]
    [InlineData("ipv4", "Invalid IPv4 address.")]
    [InlineData("ipv6", "Invalid IPv6 address.")]
    [InlineData("cidr", "Invalid CIDR range.")]
    [InlineData("hostname", "Invalid host name.")]
    [InlineData("jwt", "Invalid token.")]
    [InlineData("credit_card", "Invalid card number.")]
    [InlineData("iban", "Invalid IBAN.")]
    public void Each_Format_Reports_Its_Own_Name_And_Sentence(string format, string message)
    {
        var schema = format switch
        {
            "ipv4" => Theo.String().Ipv4(),
            "ipv6" => Theo.String().Ipv6(),
            "cidr" => Theo.String().Cidr(),
            "hostname" => Theo.String().Hostname(),
            "jwt" => Theo.String().Jwt(),
            "credit_card" => Theo.String().CreditCard(),
            _ => Theo.String().Iban(),
        };

        var error = Assert.Single(schema.SafeParse("definitely not valid").Errors);

        Assert.Equal(format, error.Format);
        Assert.Equal(message, error.Message);
    }
}
