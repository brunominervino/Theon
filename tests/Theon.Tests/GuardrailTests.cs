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
    // Raised from 144 KiB deliberately, and this is the record of why. Describing schemas, generating
    // documents, the transform pipeline and seven more formats added about fifteen kilobytes between
    // them, which took the assembly to 92% of the old ceiling -- close enough that the next small
    // addition would have tripped it for no reason worth discussing. Still nowhere near enough room to
    // hide a dependency being pulled in or a source generator running away, which is what this is for.
    private const long AssemblyByteCeiling = 160 * 1024;

    // Measured, not guessed. Raising either number is a decision about a support
    // obligation that lasts for ever, which is why it belongs in a diff.
    private const int PublicTypeCount = 51;

    private const int PublicMemberCount = 298;

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
