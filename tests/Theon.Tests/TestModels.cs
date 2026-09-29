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
