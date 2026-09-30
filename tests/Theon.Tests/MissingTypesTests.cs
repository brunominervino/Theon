using Theon.Errors;

namespace Theon.Tests;

/// <summary>
/// Durations, addresses, sets, distinctness — and two shapes that needed no new type at all.
/// </summary>
public class MissingTypesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void TimeSpan_Bounds()
    {
        var schema = Theo.TimeSpan().Min(TimeSpan.FromSeconds(1)).Max(TimeSpan.FromMinutes(5));

        Assert.True(schema.IsValid(TimeSpan.FromSeconds(30)));
        Assert.True(schema.IsValid(TimeSpan.FromSeconds(1)));
        Assert.True(schema.IsValid(TimeSpan.FromMinutes(5)));
        Assert.False(schema.IsValid(TimeSpan.FromMilliseconds(500)));
        Assert.False(schema.IsValid(TimeSpan.FromMinutes(6)));
    }

    // The mistake this schema mostly exists to catch: two dates subtracted the wrong way round.
    [Fact]
    public void TimeSpan_Positive_Rejects_Zero_And_Negative()
    {
        var schema = Theo.TimeSpan().Positive();

        Assert.True(schema.IsValid(TimeSpan.FromTicks(1)));
        Assert.False(schema.IsValid(TimeSpan.Zero));
        Assert.False(schema.IsValid(TimeSpan.FromSeconds(-1)));

        var earlier = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.False(schema.IsValid(later - earlier));
    }

    [Fact]
    public void TimeSpan_NonNegative_Allows_Zero()
    {
        Assert.True(Theo.TimeSpan().NonNegative().IsValid(TimeSpan.Zero));
        Assert.False(Theo.TimeSpan().NonNegative().IsValid(TimeSpan.FromSeconds(-1)));
    }

    // A duration is not a point in time, so its message is the one a number gets. "Must be later
    // than 00:00:05" would be wrong about a timeout.
    [Fact]
    public void TimeSpan_Reads_Like_A_Number_Not_A_Date()
    {
        var result = Theo.TimeSpan().Min(TimeSpan.FromSeconds(5)).SafeParse(TimeSpan.FromSeconds(1));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationOrigin.Number, error.Origin);
        Assert.Equal("Must be greater than or equal to 00:00:05.", error.Message);
    }

    [Fact]
    public void TimeSpan_AllowNull_And_Required()
    {
        Assert.True(Theo.TimeSpan().Positive().AllowNull().IsValid(null));
        Assert.False(Theo.TimeSpan().Positive().Required().IsValid(null));
    }

    [Fact]
    public void Uri_Absolute()
    {
        var schema = Theo.Uri().Absolute();

        Assert.True(schema.IsValid(new Uri("https://example.com/hook")));
        Assert.False(schema.IsValid(new Uri("/api/things", UriKind.Relative)));
    }

    [Fact]
    public void Uri_Scheme()
    {
        var schema = Theo.Uri().Scheme("https");

        Assert.True(schema.IsValid(new Uri("https://example.com")));
        Assert.False(schema.IsValid(new Uri("http://example.com")));
        Assert.True(Theo.Uri().Scheme("http", "https").IsValid(new Uri("http://example.com")));
    }

    // Reading Scheme off a relative Uri throws, so this rule answers instead of letting the
    // exception out. That is what makes it safe to write without Absolute in front of it.
    [Fact]
    public void Uri_Scheme_Reports_A_Relative_Address_Rather_Than_Throwing()
    {
        var result = Theo.Uri().Scheme("https").SafeParse(new Uri("/api", UriKind.Relative));

        Assert.Equal("uri_scheme", Assert.Single(result.Errors).Format);
    }

    // Absolute aborts the rules after it, because every other question about a relative Uri throws.
    // One error, not two, and not an exception.
    [Fact]
    public void Uri_A_Relative_Address_Reports_Once()
    {
        var result = Theo.Uri().Absolute().Scheme("https").SafeParse(new Uri("/api", UriKind.Relative));

        var error = Assert.Single(result.Errors);
        Assert.Equal("absolute_uri", error.Format);
        Assert.Equal("Must be an absolute address.", error.Message);
    }

    [Fact]
    public void Uri_Names_The_Schemes_It_Accepts()
    {
        var result = Theo.Uri().Scheme("http", "https").SafeParse(new Uri("ftp://example.com"));

        Assert.Equal("Scheme must be one of: http, https.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Uri_Rejects_Null_And_AllowNull_Accepts_It()
    {
        Assert.False(Theo.Uri().IsValid(null!));
        Assert.True(Theo.Uri().Absolute().AllowNull().IsValid(null));
    }

    [Fact]
    public void Uri_Refuses_A_Scheme_Rule_That_Allows_Nothing() =>
        Assert.Throws<ArgumentException>(() => Theo.Uri().Scheme());

    // Schemas are immutable, and that has to hold against the array a caller passed in as well.
    [Fact]
    public void Uri_Copies_The_Schemes_It_Was_Given()
    {
        var schemes = new[] { "https" };
        var schema = Theo.Uri().Scheme(schemes);

        schemes[0] = "ftp";

        Assert.True(schema.IsValid(new Uri("https://example.com")));
        Assert.False(schema.IsValid(new Uri("ftp://example.com")));
    }

    [Fact]
    public void Set_Validates_Every_Member_And_The_Count()
    {
        var schema = Theo.Set(Theo.String().MaxLength(5)).MaxCount(3);

        Assert.True(schema.IsValid(new HashSet<string> { "a", "bb" }));
        Assert.False(schema.IsValid(new HashSet<string> { "a", "toolongvalue" }));
        Assert.False(schema.IsValid(new HashSet<string> { "a", "b", "c", "d" }));
    }

    // A set has no positions and its enumeration order is not something to report against, so a
    // member's failure lands on the set's own path. Two bad members, two errors, both at Tags.
    [Fact]
    public void Set_Reports_At_Its_Own_Path_And_Never_At_An_Index()
    {
        var schema = Theo.Object<Article>()
            .Field(x => x.Tags, Theo.Set(Theo.String().MaxLength(3)));

        var value = new Article { Tags = new HashSet<string> { "ok", "toolong", "alsotoolong" } };

        var result = schema.SafeParse(value);

        Assert.Equal(2, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal("Tags", error.Path.ToString()));
    }

    [Fact]
    public void Set_Rejects_Null_And_AllowNull_Accepts_It()
    {
        Assert.False(Theo.Set(Theo.String()).IsValid(null!));
        Assert.True(Theo.Set(Theo.String()).AllowNull().IsValid(null));
    }

    [Fact]
    public void Set_NotEmpty_And_Count()
    {
        Assert.False(Theo.Set(Theo.String()).NotEmpty().IsValid(new HashSet<string>()));
        Assert.True(Theo.Set(Theo.String()).Count(2).IsValid(new HashSet<string> { "a", "b" }));
        Assert.False(Theo.Set(Theo.String()).Count(2).IsValid(new HashSet<string> { "a" }));
    }

    [Fact]
    public async Task Set_Carries_Asynchrony_To_Its_Members()
    {
        var schema = Theo.Set(
            Theo.String().RefineAsync(
                static (value, _) => new ValueTask<bool>(value != "taken"),
                "Already in use."));

        var free = new HashSet<string> { "free" };
        var taken = new HashSet<string> { "taken" };

        Assert.True((await schema.SafeParseAsync(free, cancellationToken: Ct)).IsSuccess);
        Assert.False((await schema.SafeParseAsync(taken, cancellationToken: Ct)).IsSuccess);
    }

    // A frozen set and an IReadOnlySet bind without a second overload, because both are read-only
    // collections. An ISet does not, and that is documented rather than worked around.
    [Fact]
    public void Set_Binds_Any_Read_Only_Collection()
    {
        var schema = Theo.Set(Theo.String().NotEmpty());

        IReadOnlySet<string> readOnly = new HashSet<string> { "a" };

        Assert.True(schema.IsValid(readOnly));
        Assert.True(schema.IsValid(System.Collections.Frozen.FrozenSet.ToFrozenSet(["a", "b"])));
    }

    [Fact]
    public void Unique_Rejects_A_Repeat()
    {
        var schema = Theo.Collection(Theo.String()).Unique();

        Assert.True(schema.IsValid(new[] { "a", "b", "c" }));
        Assert.False(schema.IsValid(new[] { "a", "b", "a" }));
    }

    // The repeat is reported at its own index, not once about the list, so a form can mark the row.
    // The first occurrence is never the one reported.
    [Fact]
    public void Unique_Reports_The_Repeat_And_Not_The_Original()
    {
        var result = Theo.Collection(Theo.String()).Unique().SafeParse(new[] { "a", "b", "a", "b" });

        Assert.Equal(["[2]", "[3]"], result.Errors.Select(e => e.Path.ToString()));
        Assert.All(result.Errors, error => Assert.Equal(ValidationErrorCode.Duplicate, error.Code));
        Assert.All(result.Errors, error => Assert.Equal("Already listed.", error.Message));
    }

    [Fact]
    public void Unique_Has_Nothing_To_Say_About_A_Short_List()
    {
        Assert.True(Theo.Collection(Theo.String()).Unique().IsValid([]));
        Assert.True(Theo.Collection(Theo.String()).Unique().IsValid(["only"]));
    }

    // Past a threshold the rule swaps a quadratic scan for a set. The answer has to be the same one
    // either way, which is the whole reason the threshold is not part of the contract.
    [Fact]
    public void Unique_Gives_The_Same_Answer_Either_Side_Of_Its_Threshold()
    {
        var schema = Theo.Collection(Theo.Int()).Unique();

        var distinct = Enumerable.Range(0, 200).ToArray();
        Assert.True(schema.IsValid(distinct));

        var withOneRepeat = Enumerable.Range(0, 200).ToArray();
        withOneRepeat[199] = 0;
        var result = schema.SafeParse(withOneRepeat);

        Assert.Equal("[199]", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void Unique_Compares_The_Values_The_List_Holds()
    {
        // Distinct as written, the same value once trimmed and lowercased. Unique compares what the
        // list holds, not what the element schema would normalize it into, because an element rule
        // never writes back to the list. See ObjectSchema's remarks on the same point.
        var schema = Theo.Collection(Theo.String().Trim().ToLowerInvariant()).Unique();

        Assert.True(schema.IsValid(new[] { "Tag", " tag " }));
    }

    // Tuples needed no new type. ObjectSchema constrains T to notnull, a tuple is a struct, and the
    // field name comes from the source text of the accessor — so this already worked.
    [Fact]
    public void A_Named_Tuple_Is_Just_An_Object()
    {
        var schema = Theo.Object<(string Name, int Age)>()
            .Field(x => x.Name, Theo.String().MinLength(2))
            .Field(x => x.Age, Theo.Int().Min(18));

        Assert.True(schema.IsValid(("Ada", 36)));

        var result = schema.SafeParse(("A", 5));

        Assert.Equal(["Name", "Age"], result.Errors.Select(e => e.Path.ToString()));
    }

    [Fact]
    public void A_Positional_Tuple_Reports_By_Its_Item_Names()
    {
        var schema = Theo.Object<(string, int)>()
            .Field(x => x.Item1, Theo.String().MinLength(2))
            .Field(x => x.Item2, Theo.Int().Min(18));

        Assert.Equal("Item2", Assert.Single(schema.SafeParse(("Ada", 5)).Errors).Path.ToString());
    }

    // A char needed no new type either. There is nothing to measure or match on one — only which
    // values are allowed — and Literal with OneOf says exactly that.
    [Fact]
    public void A_Char_Is_Expressed_With_Literal_And_OneOf()
    {
        var flag = Theo.OneOf("Must be Y or N.", Theo.Literal('Y'), Theo.Literal('N'));

        Assert.True(flag.IsValid('Y'));
        Assert.True(flag.IsValid('N'));
        Assert.False(flag.IsValid('x'));
        Assert.Equal("Must be Y or N.", Assert.Single(flag.SafeParse('x').Errors).Message);
    }
}
