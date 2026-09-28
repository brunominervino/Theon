using Theon.Errors;

namespace Theon.Tests;

public class ObjectSchemaTests
{
    private static readonly Schema<CreateUserRequest> UserSchema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().Trim().MinLength(3).MaxLength(100))
            .Field(x => x.Email, Theo.String().Trim().ToLowerInvariant().Email())
            .Field(x => x.Age, Theo.Int().Min(18).Max(120))
            .Field(x => x.CompanyId, Theo.Guid().NotEmpty());

    private static CreateUserRequest Valid() => new()
    {
        Name = "Ada Lovelace",
        Email = "ada@example.com",
        Age = 36,
        CompanyId = Guid.NewGuid(),
    };

    [Fact]
    public void A_Valid_Object_Passes()
    {
        var result = UserSchema.SafeParse(Valid());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void The_Error_Path_Names_The_Property()
    {
        var request = Valid();
        request.Email = "not-an-email";

        var result = UserSchema.SafeParse(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Email", error.Path.ToString());
    }

    [Fact]
    public void Every_Failing_Field_Is_Reported()
    {
        var request = new CreateUserRequest
        {
            Name = "A",
            Email = "bad",
            Age = 5,
            CompanyId = Guid.Empty,
        };

        var result = UserSchema.SafeParse(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.Errors.Count);
        Assert.Equal(
            new[] { "Name", "Email", "Age", "CompanyId" },
            result.Errors.Select(e => e.Path.ToString()).ToArray());
    }

    [Fact]
    public void StopOnFirstError_Reports_Only_One()
    {
        var request = new CreateUserRequest { Name = "A", Email = "bad", Age = 5 };

        var result = UserSchema.SafeParse(request, new ParseOptions { StopOnFirstError = true });

        Assert.Single(result.Errors);
    }

    [Fact]
    public void Nested_Objects_Produce_A_Dotted_Path()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Address, Theo.Object<Address>()
                .Field(a => a.ZipCode, Theo.String().Length(8))
                .AllowNull());

        var request = Valid();
        request.Address = new Address { ZipCode = "123" };

        var result = schema.SafeParse(request);

        Assert.Equal("Address.ZipCode", Assert.Single(result.Errors).Path.ToString());
    }

    [Fact]
    public void An_Explicit_Field_Name_Overrides_The_Inferred_One()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field("email_address", x => x.Email, Theo.String().Email());

        var request = Valid();
        request.Email = "bad";

        Assert.Equal("email_address", Assert.Single(schema.SafeParse(request).Errors).Path.ToString());
    }

    [Fact]
    public void Null_Fails_As_An_Invalid_Type()
    {
        var result = UserSchema.SafeParse(null!);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCode.InvalidType, error.Code);
        Assert.True(error.Path.IsRoot);
    }

    [Fact]
    public void Parse_Throws_With_Every_Error_Attached()
    {
        var request = new CreateUserRequest { Name = "A", Email = "bad", Age = 5 };

        var exception = Assert.Throws<SchemaValidationException>(() => UserSchema.Parse(request));

        Assert.Equal(4, exception.Errors.Count);
        Assert.Contains("Email", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Object_Is_Never_Mutated_By_A_Transforming_Rule()
    {
        var request = Valid();
        request.Name = "  Ada  ";

        Assert.True(UserSchema.SafeParse(request).IsSuccess);
        Assert.Equal("  Ada  ", request.Name);
    }
}
