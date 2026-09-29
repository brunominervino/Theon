namespace Theon.Tests;

/// <summary>
/// A field that is optional in general but required once another field takes a particular value.
/// </summary>
public class ConditionalValidationTests
{
    private static readonly Schema<TaskItem> Schema =
        Theo.Object<TaskItem>()
            .Field(x => x.Title, Theo.String().Trim().NotEmpty())
            .Field(x => x.Status, Theo.Enum<TaskStatus>())
            .Refine(
                x => x.Status != TaskStatus.Completed || x.CompletedAt is not null,
                x => x.CompletedAt,
                "A completion date is required once the task is completed.");

    [Fact]
    public void Pending_Without_A_Completion_Date_Is_Fine()
    {
        var value = new TaskItem { Title = "Write docs", Status = TaskStatus.Pending };

        Assert.True(Schema.IsValid(value));
    }

    [Fact]
    public void Completed_Without_A_Completion_Date_Fails_On_That_Field()
    {
        var value = new TaskItem { Title = "Write docs", Status = TaskStatus.Completed };

        var error = Assert.Single(Schema.SafeParse(value).Errors);

        Assert.Equal("CompletedAt", error.Path.ToString());
        Assert.Equal("A completion date is required once the task is completed.", error.Message);
    }

    [Fact]
    public void Completed_With_A_Completion_Date_Passes()
    {
        var value = new TaskItem
        {
            Title = "Write docs",
            Status = TaskStatus.Completed,
            CompletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.True(Schema.IsValid(value));
    }

    [Fact]
    public void A_Completion_Date_On_A_Pending_Task_Is_Still_Allowed()
    {
        // The rule says "completed implies a date", not "a date implies completed".
        var value = new TaskItem
        {
            Title = "Write docs",
            Status = TaskStatus.Pending,
            CompletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.True(Schema.IsValid(value));
    }

    [Fact]
    public void The_Conditional_Rule_Waits_For_The_Fields_To_Be_Valid()
    {
        // Title is empty, so only that is reported. A rule about Status and CompletedAt written
        // against valid data should not run over data already known to be broken.
        var value = new TaskItem { Title = "  ", Status = TaskStatus.Completed };

        var error = Assert.Single(Schema.SafeParse(value).Errors);

        Assert.Equal("Title", error.Path.ToString());
    }
}
