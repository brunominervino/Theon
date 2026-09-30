namespace Theon.Tests;

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }

    public Guid CompanyId { get; set; }

    public Address? Address { get; set; }
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;

    public string ZipCode { get; set; } = string.Empty;
}

public sealed class SignUp
{
    public string Password { get; set; } = string.Empty;

    public string PasswordConfirmation { get; set; } = string.Empty;
}

public sealed class Level1
{
    public Level2 Child { get; set; } = new();
}

public sealed class Level2
{
    public Level3 Child { get; set; } = new();
}

public sealed class Level3
{
    public string Value { get; set; } = string.Empty;
}

public sealed class Node
{
    public Node? Child { get; set; }

    public string Value { get; set; } = string.Empty;
}

public sealed class Mailing
{
    public IReadOnlyList<string> Recipients { get; set; } = [];
}

public sealed class Booking
{
    public DateTime CheckIn { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public struct Coordinates
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public record struct Money(decimal Amount, string Currency);

public readonly record struct Temperature(double Celsius);

public sealed class Reading
{
    public Coordinates Where { get; set; }

    public Temperature? Value { get; set; }
}

public record struct Span(DateOnly Start, DateOnly End);

public enum TaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
}

public sealed class TaskItem
{
    public string Title { get; set; } = string.Empty;

    public TaskStatus Status { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public sealed class Ticket
{
    public string Title { get; set; } = string.Empty;

    public TaskStatus Status { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ClosedBy { get; set; }

    public string? Resolution { get; set; }
}

public sealed class Comment
{
    public string Body { get; set; } = string.Empty;

    public IReadOnlyList<Comment> Replies { get; set; } = [];
}

public abstract class Payment
{
    public decimal Amount { get; set; }
}

public sealed class PixPayment : Payment
{
    public string Key { get; set; } = string.Empty;
}

public sealed class CardPayment : Payment
{
    public string Number { get; set; } = string.Empty;

    public string Holder { get; set; } = string.Empty;
}

public sealed class BoletoPayment : Payment
{
    public string Barcode { get; set; } = string.Empty;
}

public sealed class Order
{
    public Payment Payment { get; set; } = new PixPayment();
}

public interface IInstrument
{
    string Label { get; }
}

public sealed class Wire : IInstrument
{
    public string Label { get; set; } = string.Empty;
}

// The same domain modelled as one flat class, which is what a service that does not use polymorphic
// serialization sends. Its rules are conditional on a property, not on a type.
public sealed class FlatPayment
{
    public string Kind { get; set; } = string.Empty;

    public string? PixKey { get; set; }

    public string? CardNumber { get; set; }
}

public sealed class Query
{
    public string Sort { get; set; } = "asc";

    public string PageSize { get; set; } = "20";
}

public sealed class Article
{
    public string Title { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Tags { get; set; } = new HashSet<string>();
}

public sealed class Catalogue
{
    public IReadOnlyDictionary<string, decimal> Prices { get; set; } =
        new Dictionary<string, decimal>();
}
