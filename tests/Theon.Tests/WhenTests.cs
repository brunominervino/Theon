using Theon.Errors;

namespace Theon.Tests;

public class WhenTests
{
    private static readonly DateTime Completed = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The short form: one condition, one requirement, reported on the field to correct.
    private static readonly Schema<TaskItem> ShortForm =
        Theo.Object<TaskItem>()
            .Field(x => x.Title, Theo.String().NotEmpty())
            .Field(x => x.Status, Theo.Enum<TaskStatus>())
            .When(
                x => x.Status == TaskStatus.Completed,
                x => x.CompletedAt is not null,
                x => x.CompletedAt,
                "A completion date is required once the task is completed.");

    [Fact]
    public void The_Requirement_Is_Skipped_When_The_Condition_Is_False() =>
        Assert.True(ShortForm.IsValid(new TaskItem { Title = "x", Status = TaskStatus.Pending }));

    [Fact]
    public void The_Requirement_Applies_When_The_Condition_Holds()
    {
        var result = ShortForm.SafeParse(new TaskItem { Title = "x", Status = TaskStatus.Completed });

        var error = Assert.Single(result.Errors);
        Assert.Equal("CompletedAt", error.Path.ToString());
        Assert.Equal("A completion date is required once the task is completed.", error.Message);
    }

    [Fact]
    public void The_Requirement_Is_Satisfied_When_Met() =>
        Assert.True(ShortForm.IsValid(new TaskItem
        {
            Title = "x",
            Status = TaskStatus.Completed,
            CompletedAt = Completed,
        }));

    [Fact]
    public void The_Implication_Does_Not_Run_Backwards() =>
        // Completed implies a date. A date does not imply completed.
        Assert.True(ShortForm.IsValid(new TaskItem
        {
            Title = "x",
            Status = TaskStatus.Pending,
            CompletedAt = Completed,
        }));

    [Fact]
    public void The_Pathless_Overload_Reports_At_The_Root()
    {
        var schema = Theo.Object<TaskItem>()
            .When(x => x.Status == TaskStatus.Completed, x => x.CompletedAt is not null, "Needs a date.");

        var error = Assert.Single(schema.SafeParse(new TaskItem { Status = TaskStatus.Completed }).Errors);

        Assert.True(error.Path.IsRoot);
        Assert.Equal("Needs a date.", error.Message);
    }

    // The block form: one condition unlocking several requirements at once.
    private static readonly Schema<Ticket> BlockForm =
        Theo.Object<Ticket>()
            .Field(x => x.Title, Theo.String().NotEmpty())
            .Field(x => x.Status, Theo.Enum<TaskStatus>())
            .When(x => x.Status == TaskStatus.Completed, rules => rules
                .Field(x => x.CompletedAt, Theo.DateTime().RequireUtc().Required("A completion date is required."))
                .Field(x => x.ClosedBy, Theo.String().NotEmpty().Required("Someone has to be named."))
                .Field(x => x.Resolution, Theo.String().MinLength(10).Required("Explain the resolution.")));

    [Fact]
    public void The_Block_Is_Skipped_Entirely_When_The_Condition_Is_False() =>
        Assert.True(BlockForm.IsValid(new Ticket { Title = "x", Status = TaskStatus.InProgress }));

    [Fact]
    public void Every_Requirement_In_The_Block_Is_Reported()
    {
        var result = BlockForm.SafeParse(new Ticket { Title = "x", Status = TaskStatus.Completed });

        Assert.Equal(3, result.Errors.Count);
        Assert.Equal(
            new[] { "CompletedAt", "ClosedBy", "Resolution" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void The_Block_Passes_When_Everything_Is_Supplied() =>
        Assert.True(BlockForm.IsValid(new Ticket
        {
            Title = "x",
            Status = TaskStatus.Completed,
            CompletedAt = Completed,
            ClosedBy = "ada",
            Resolution = "Fixed the underlying race condition.",
        }));

    [Fact]
    public void Rules_Inside_The_Block_Are_Full_Rules_Not_Just_Presence_Checks()
    {
        // Present, but the resolution is too short and the date is not UTC.
        var result = BlockForm.SafeParse(new Ticket
        {
            Title = "x",
            Status = TaskStatus.Completed,
            CompletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local),
            ClosedBy = "ada",
            Resolution = "too short",
        });

        Assert.Equal(
            new[] { "CompletedAt", "Resolution" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void Blocks_Can_Be_Stacked_For_Different_Conditions()
    {
        var schema = Theo.Object<Ticket>()
            .Field(x => x.Status, Theo.Enum<TaskStatus>())
            .When(x => x.Status == TaskStatus.Completed, rules => rules
                .Field(x => x.CompletedAt, Theo.DateTime().Required("Date required.")))
            .When(x => x.Status == TaskStatus.InProgress, rules => rules
                .Field(x => x.ClosedBy, Theo.String().NotEmpty().Required("Assignee required.")));

        Assert.Equal(
            "Date required.",
            Assert.Single(schema.SafeParse(new Ticket { Status = TaskStatus.Completed }).Errors).Message);

        Assert.Equal(
            "Assignee required.",
            Assert.Single(schema.SafeParse(new Ticket { Status = TaskStatus.InProgress }).Errors).Message);
    }

    [Fact]
    public void Required_Rejects_Null_And_Reports_An_Invalid_Type()
    {
        var schema = Theo.Object<Ticket>()
            .Field(x => x.CompletedAt, Theo.DateTime().Required());

        var error = Assert.Single(schema.SafeParse(new Ticket()).Errors);

        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.Equal("CompletedAt", error.Path.ToString());
    }

    [Fact]
    public void Required_On_A_Reference_Type_Keeps_The_Nullable_Shape()
    {
        Schema<string?> schema = Theo.String().MinLength(3).Required("Required.");

        Assert.False(schema.SafeParse(null).IsSuccess);
        Assert.False(schema.SafeParse("ab").IsSuccess);
        Assert.True(schema.SafeParse("abc").IsSuccess);
    }
}
