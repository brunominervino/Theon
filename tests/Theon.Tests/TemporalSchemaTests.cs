using Microsoft.Extensions.Time.Testing;
using Theon.Errors;

namespace Theon.Tests;

public class TemporalSchemaTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static FakeTimeProvider Clock() => new(Fixed);

    [Fact]
    public void DateTime_Min_And_Max_Are_Inclusive()
    {
        var schema = Theo.DateTime()
            .Min(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Max(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(schema.IsValid(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.True(schema.IsValid(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(schema.IsValid(new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void RequireUtc_Rejects_Other_Kinds()
    {
        var schema = Theo.DateTime().RequireUtc();

        Assert.True(schema.IsValid(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(schema.IsValid(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)));
        Assert.False(schema.IsValid(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void RequireUtc_Reports_What_It_Got()
    {
        var result = Theo.DateTime().RequireUtc()
            .SafeParse(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("Unspecified", error.Info.Received);
    }

    [Fact]
    public void A_Failed_Kind_Check_Stops_The_Bounds_From_Reporting()
    {
        // Once the kind is wrong, every bound below is comparing an instant that does not mean
        // what it appears to, so their complaints would be noise.
        var schema = Theo.DateTime()
            .RequireUtc()
            .Min(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = schema.SafeParse(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local));

        // Only the kind failure, not the bound it would also have tripped.
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
    }

    [Fact]
    public void InPast_Is_Measured_Against_The_Injected_Clock()
    {
        var schema = Theo.DateTime(Clock()).InPast();

        Assert.True(schema.IsValid(Fixed.UtcDateTime.AddDays(-1)));
        Assert.False(schema.IsValid(Fixed.UtcDateTime.AddDays(1)));
    }

    [Fact]
    public void InPast_Excludes_The_Instant_Itself()
    {
        Assert.False(Theo.DateTime(Clock()).InPast().IsValid(Fixed.UtcDateTime));
    }

    [Fact]
    public void InFuture_Is_Measured_Against_The_Injected_Clock()
    {
        var schema = Theo.DateTime(Clock()).InFuture();

        Assert.True(schema.IsValid(Fixed.UtcDateTime.AddSeconds(1)));
        Assert.False(schema.IsValid(Fixed.UtcDateTime.AddSeconds(-1)));
    }

    [Fact]
    public void Advancing_The_Clock_Changes_The_Answer()
    {
        // The point of injecting the clock: the same value flips from future to past without the
        // test sleeping, and without depending on what the machine thinks the time is.
        var clock = Clock();
        var schema = Theo.DateTime(clock).InPast();
        var instant = Fixed.UtcDateTime.AddHours(1);

        Assert.False(schema.IsValid(instant));

        clock.Advance(TimeSpan.FromHours(2));

        Assert.True(schema.IsValid(instant));
    }

    [Fact]
    public void DateTimeOffset_Compares_Instants_Across_Zones()
    {
        // Same instant, different offsets. A bound must not care which zone wrote it.
        var noonUtc = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var nineInBrasilia = new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.FromHours(-3));

        var schema = Theo.DateTimeOffset().Min(noonUtc).Max(noonUtc);

        Assert.True(schema.IsValid(nineInBrasilia));
    }

    [Fact]
    public void DateTimeOffset_InPast_And_InFuture()
    {
        var schema = Theo.DateTimeOffset(Clock());

        Assert.True(schema.InPast().IsValid(Fixed.AddMinutes(-1)));
        Assert.False(schema.InPast().IsValid(Fixed.AddMinutes(1)));
        Assert.True(schema.InFuture().IsValid(Fixed.AddMinutes(1)));
    }

    [Fact]
    public void DateOnly_Bounds_And_Past()
    {
        var schema = Theo.DateOnly(Clock());

        Assert.True(schema.Min(new DateOnly(2026, 1, 1)).IsValid(new DateOnly(2026, 6, 1)));
        Assert.False(schema.Min(new DateOnly(2026, 1, 1)).IsValid(new DateOnly(2025, 1, 1)));
        Assert.True(schema.InPast().IsValid(new DateOnly(2026, 6, 14)));
        Assert.False(schema.InPast().IsValid(new DateOnly(2026, 6, 16)));
    }

    [Fact]
    public void TimeOnly_Bounds()
    {
        var schema = Theo.TimeOnly().Min(new TimeOnly(9, 0)).Max(new TimeOnly(17, 0));

        Assert.True(schema.IsValid(new TimeOnly(12, 0)));
        Assert.False(schema.IsValid(new TimeOnly(8, 59)));
        Assert.False(schema.IsValid(new TimeOnly(17, 1)));
    }

    [Fact]
    public void Temporal_Schemas_Compose_Into_Objects()
    {
        var schema = Theo.Object<Booking>()
            .Field(x => x.CheckIn, Theo.DateTime(Clock()).RequireUtc().InFuture())
            .Field(x => x.CreatedAt, Theo.DateTimeOffset(Clock()).InPast());

        var value = new Booking
        {
            CheckIn = Fixed.UtcDateTime.AddDays(-1),
            CreatedAt = Fixed.AddDays(1),
        };

        var result = schema.SafeParse(value);

        Assert.Equal(
            new[] { "CheckIn", "CreatedAt" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void AllowNull_Works_For_Temporal_Types()
    {
        Assert.True(Theo.DateTime().AllowNull().SafeParse(null).IsSuccess);
        Assert.True(Theo.DateTimeOffset().AllowNull().SafeParse(null).IsSuccess);
        Assert.True(Theo.DateOnly().AllowNull().SafeParse(null).IsSuccess);
        Assert.True(Theo.TimeOnly().AllowNull().SafeParse(null).IsSuccess);
    }
}
