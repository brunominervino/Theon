namespace Theon.Sample;

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
