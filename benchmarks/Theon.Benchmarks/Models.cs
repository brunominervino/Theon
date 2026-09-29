namespace Theon.Benchmarks;

public sealed class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int Age { get; set; }

    public Guid CompanyId { get; set; }

    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string ZipCode { get; set; } = string.Empty;
}

public enum UserStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
}
