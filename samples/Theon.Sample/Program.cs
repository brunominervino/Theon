using Theon;
using Theon.Errors;
using Theon.Sample;

// Built once. Immutable, and safe to share across every request thread.
var addressSchema = Theo.Object<Address>()
    .Field(a => a.Street, Theo.String().Trim().MinLength(3))
    .Field(a => a.ZipCode, Theo.String().Trim().Length(8, "A postcode has 8 digits."));

var userSchema = Theo.Object<CreateUserRequest>()
    .Field(x => x.Name, Theo.String().Trim().MinLength(3).MaxLength(100))
    .Field(x => x.Email, Theo.String().Trim().ToLowerInvariant().Email())
    .Field(x => x.Age, Theo.Int().Min(18).Max(120))
    .Field(x => x.CompanyId, Theo.Guid().NotEmpty())
    .Field(x => x.Address, addressSchema.AllowNull());

Console.WriteLine("--- a valid request ---");
Report(userSchema.SafeParse(new CreateUserRequest
{
    Name = "  Ada Lovelace  ",
    Email = "  ADA@Example.COM  ",
    Age = 36,
    CompanyId = Guid.NewGuid(),
    Address = new Address { Street = "Main Street", ZipCode = "12345678" },
}));

Console.WriteLine();
Console.WriteLine("--- every field wrong, including a nested one ---");
Report(userSchema.SafeParse(new CreateUserRequest
{
    Name = "A",
    Email = "not-an-email",
    Age = 7,
    CompanyId = Guid.Empty,
    Address = new Address { Street = "x", ZipCode = "123" },
}));

Console.WriteLine();
Console.WriteLine("--- messages in another language, from the same codes ---");
var portuguese = new ParseOptions
{
    MessageProvider = static (in ValidationErrorInfo error) => error switch
    {
        { Code: ValidationErrorCode.TooSmall, Origin: ValidationOrigin.Text } =>
            $"Informe ao menos {error.Minimum} caractere(s).",
        { Code: ValidationErrorCode.TooSmall, Origin: ValidationOrigin.Number } =>
            $"Deve ser maior ou igual a {error.Minimum}.",
        { Code: ValidationErrorCode.InvalidFormat, Format: "email" } => "E-mail invalido.",
        _ => null,
    },
};

Report(userSchema.SafeParse(
    new CreateUserRequest { Name = "A", Email = "nope", Age = 7, CompanyId = Guid.NewGuid() },
    portuguese));

Console.WriteLine();
Console.WriteLine("--- a cross-field rule, reported on the field the user must fix ---");
var signUpSchema = Theo.Object<SignUp>()
    .Field(x => x.Password, Theo.String().MinLength(8))
    .Field(x => x.PasswordConfirmation, Theo.String())
    .Refine(
        x => x.Password == x.PasswordConfirmation,
        x => x.PasswordConfirmation,
        "The passwords do not match.");

Report(signUpSchema.SafeParse(new SignUp
{
    Password = "correct horse",
    PasswordConfirmation = "battery staple",
}));

Console.WriteLine();
Console.WriteLine("--- a schema that changes the type on its way out ---");
Schema<string, int> port = Theo.String().Trim().Transform(int.Parse);
Console.WriteLine($"parsed port: {port.Parse("  8080  ")}");

static void Report<T>(ParseResult<T> result)
{
    if (result.IsSuccess)
    {
        Console.WriteLine("  ok");
        return;
    }

    foreach (var error in result.Errors)
    {
        var where = error.Path.IsRoot ? "(root)" : error.Path.ToString();
        Console.WriteLine($"  {where,-24} {error.Code,-14} {error.Message}");
    }
}

namespace Theon.Sample
{
    public sealed class SignUp
    {
        public string Password { get; set; } = string.Empty;

        public string PasswordConfirmation { get; set; } = string.Empty;
    }
}
