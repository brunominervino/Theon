using System.Diagnostics;
using System.Reflection;

namespace Theon.Tests;

/// <summary>
/// Tripwires for the three things that are easy to lose without noticing: the size of the assembly,
/// the size of the public surface, and the linear-time guarantee on the patterns.
/// </summary>
/// <remarks>
/// These tests are meant to fail when a number is exceeded, and the fix is sometimes to raise the
/// number. That is the point: raising it is a decision someone makes on purpose, in a diff, rather
/// than a drift nobody sees.
/// </remarks>
public class GuardrailTests
{
    // Raised twice, and each raise is recorded here rather than discovered later.
    //
    // 144 -> 160 KiB: describing schemas, generating documents, the transform pipeline and seven more
    // formats added about fifteen kilobytes between them, which took the assembly to 92% of the old
    // ceiling -- close enough that the next small addition would have tripped it for no reason worth
    // discussing.
    //
    // 160 -> 192 KiB: reading a document back into a schema. That is a reader, a validator over the
    // description model and a table of formats, and it took the assembly to 163,328 bytes against a
    // ceiling of 163,840 -- which passed by five hundred bytes, which is passing by luck. The feature
    // is worth its size and the ceiling was not chosen to constrain it, so the ceiling moves, on
    // purpose, here, with this sentence beside it.
    //
    // Still nowhere near enough room to hide a dependency being pulled in or a source generator
    // running away, which is what this is for.
    private const long AssemblyByteCeiling = 192 * 1024;

    // Measured, not guessed. Raising either number is a decision about a support
    // obligation that lasts for ever, which is why it belongs in a diff.
    //
    // 51 -> 54 types, in two decisions.
    //
    // DescriptionDirection, with its two values and the value__ field every enum carries, plus
    // JsonSchemaOptions.Direction. A document used to describe the input side and only the input side,
    // which is right for a request body and wrong for a response body. Four members is a cheap price
    // for the generator being usable on the half of an API it could not describe before.
    //
    // JsonSchemaAmendment and JsonSchemaNode, plus JsonSchemaOptions.Amend. UnrepresentablePolicy.Throw
    // said what a document had to leave out and gave no way to put it back, so a caller whose own
    // refinement had a perfectly good pattern was told about it and then stuck. These two are the way
    // back, and they are the reason this library can keep refusing to guess. Nine members between
    // them: four on the delegate, which every delegate type carries, four on the node, and the option.
    //
    // 54 -> 60 types, and this is the big one: the description model became public. SchemaKind,
    // SchemaDescription, PropertyDescription, DescriptionContext, SchemaDescriptionSet and the Describe
    // extension. Decision 9 kept the model internal on purpose, so that it could be changed freely; the
    // price of that was that a schema from another assembly could not describe itself -- a document
    // generated for one said "anything" -- and nobody could write a generator for a format this library
    // does not ship. Both of those are now possible, at the cost of the model being a binary
    // compatibility obligation for ever. Decision 14 is the record of that trade. Sixty-seven members,
    // most of them SchemaDescription's own: it is a data model, and a data model is mostly properties.
    //
    // 380 -> 382 members: ValidationOrigin.Bytes and SchemaDescription.ContentMediaType, both for the
    // file rules in Theon.AspNetCore. A size in bytes is a quantity like text length or an element
    // count and needs its own sentence for the same reason; contentMediaType is how 2020-12 and
    // OpenAPI 3.1 say "this string carries content of this type", which is what a multipart file is.
    // Neither mentions a file, and neither pulls ASP.NET Core anywhere near the core package.
    private const int PublicTypeCount = 60;

    // 382 -> 383: Theo.JsonSchema, which reads a document back into a schema. One method for the
    // whole of the reverse direction, because everything underneath it -- the reader, the validator,
    // the format table -- is internal, and because the thing it hands back is an ordinary Schema.
    // 383 -> 384: SchemaKind.Null, the one JSON type the enumeration was missing. Nothing here
    // produces a schema of that kind -- a C# type that can hold only null is not a type anybody
    // writes -- but a document read with Theo.JsonSchema may say it, and reading "only null" as "no
    // constraint" accepted every value where the document accepted one.
    private const int PublicMemberCount = 384;

    // Generous by three orders of magnitude. A non-backtracking match of forty thousand characters
    // takes well under a millisecond; a backtracking one over the same input does not finish this
    // century. Nothing in between is plausible, which is what makes a wall-clock assertion safe here
    // where it usually would not be.
    private static readonly TimeSpan AdversarialBudget = TimeSpan.FromSeconds(2);

    public static TheoryData<string, Schema<string>, string> AdversarialInputs()
    {
        // Each input is built to be the worst case for the shape of its own pattern: a repetition
        // nested inside another repetition, followed by a character that makes the whole match fail,
        // which is what forces a backtracking engine to try every division of the input.
        const int repeats = 20_000;

        return new TheoryData<string, Schema<string>, string>
        {
            { "email", Theo.String().Email(), "a" + string.Concat(Enumerable.Repeat(".a", repeats)) + "@" },
            { "email", Theo.String().Email(), "a@" + string.Concat(Enumerable.Repeat("a.", repeats)) },
            { "url", Theo.String().Url(), "https://" + string.Concat(Enumerable.Repeat("a.", repeats)) },
            { "url", Theo.String().Url(), "https://a" + string.Concat(Enumerable.Repeat("-a", repeats)) + "." },
            { "base64", Theo.String().Base64(), new string('A', repeats * 2) + "!" },
            { "base64url", Theo.String().Base64Url(), new string('A', repeats * 2) + "!" },
            { "hex", Theo.String().Hex(), new string('a', repeats * 2) + "!" },
            { "uuid", Theo.String().Uuid(), new string('a', repeats * 2) },
            { "e164", Theo.String().E164(), "+" + new string('1', repeats * 2) },
            { "lowercase", Theo.String().Lowercase(), new string('a', repeats * 2) + "A" },
        };
    }

    [Fact]
    public void The_Core_Assembly_Stays_Small()
    {
        var assembly = new FileInfo(typeof(Theo).Assembly.Location);

        Assert.True(
            assembly.Length <= AssemblyByteCeiling,
            $"Theon.dll is {assembly.Length} bytes, over the {AssemblyByteCeiling} byte ceiling. " +
            "Either something got much bigger than intended, or the ceiling needs raising on purpose.");
    }

    [Fact]
    public void The_Core_Assembly_Has_No_Package_Dependencies()
    {
        var referenced = typeof(Theo).Assembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name!)
            .Where(static name => !name.StartsWith("System.", StringComparison.Ordinal))
            .Where(static name => name is not ("netstandard" or "mscorlib"))
            .ToArray();

        Assert.Empty(referenced);
    }

    // The public surface is one of the three axes any change to this library is weighed on, and it is
    // the only one that cannot be re-measured after the fact: a type released by accident is released
    // for ever. Both numbers below are expected to change; changing them is the decision.
    [Fact]
    public void The_Public_Surface_Is_The_Size_It_Was_Agreed_To_Be()
    {
        var types = typeof(Theo).Assembly.GetExportedTypes();
        var members = types.Sum(static type => type
            .GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Count(static member => member is not MethodInfo { IsSpecialName: true }));

        Assert.Equal(PublicTypeCount, types.Length);
        Assert.Equal(PublicMemberCount, members);
    }

    [Theory]
    [MemberData(nameof(AdversarialInputs))]
    public void No_Pattern_Can_Be_Made_To_Take_Its_Time(
        string format,
        Schema<string> schema,
        string input)
    {
        var stopwatch = Stopwatch.StartNew();
        var accepted = schema.IsValid(input);
        stopwatch.Stop();

        Assert.False(accepted);
        Assert.True(
            stopwatch.Elapsed < AdversarialBudget,
            $"The {format} pattern took {stopwatch.ElapsedMilliseconds} ms on {input.Length} " +
            "characters. Every built-in pattern is declared NonBacktracking so that matching is " +
            "linear in the length of the input by construction; a time like this means one of them " +
            "is not, and a hostile string is now a denial of service.");
    }

    // A valid value of the same size has to be fast too. A pattern that is linear on failure and
    // quadratic on success would pass the test above and still be a way in.
    [Fact]
    public void A_Long_Valid_Value_Is_Also_Fast()
    {
        var address = "a" + string.Concat(Enumerable.Repeat(".a", 10_000)) + "@example.com";

        var stopwatch = Stopwatch.StartNew();
        var accepted = Theo.String().Email().IsValid(address);
        stopwatch.Stop();

        Assert.True(accepted);
        Assert.True(
            stopwatch.Elapsed < AdversarialBudget,
            $"Took {stopwatch.ElapsedMilliseconds} ms.");
    }
}
