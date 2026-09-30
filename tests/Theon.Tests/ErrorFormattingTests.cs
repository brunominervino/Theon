using Theon.Errors;

namespace Theon.Tests;

public class ErrorFormattingTests
{
    private static readonly Schema<CreateUserRequest> UserSchema =
        Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(3).MaxLength(5))
            .Field(x => x.Email, Theo.String().Email())
            .Field(x => x.Address, Theo.Object<Address>()
                .Field(a => a.Street, Theo.String().MinLength(3))
                .Field(a => a.ZipCode, Theo.String().Length(8))
                .AllowNull());

    private static IReadOnlyList<ValidationError> ErrorsFor(CreateUserRequest value) =>
        UserSchema.SafeParse(value).Errors;

    [Fact]
    public void Flatten_Groups_Messages_By_Field()
    {
        var errors = ErrorsFor(new CreateUserRequest
        {
            Name = "ab",
            Email = "nope",
            Address = new Address { Street = "x", ZipCode = "123" },
        });

        var flat = errors.Flatten();

        Assert.Empty(flat.RootErrors);
        Assert.Equal(
            new[] { "Name", "Email", "Address.Street", "Address.ZipCode" },
            flat.FieldErrors.Keys.ToArray());
        Assert.Equal("Invalid e-mail address.", Assert.Single(flat.FieldErrors["Email"]));
    }

    [Fact]
    public void Flatten_Keeps_Several_Messages_For_One_Field()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Name, Theo.String().MinLength(10).Email());

        var flat = schema.SafeParse(new CreateUserRequest { Name = "ab" }).Errors.Flatten();

        Assert.Equal(2, flat.FieldErrors["Name"].Count);
    }

    [Fact]
    public void Flatten_Separates_Root_Messages_From_Field_Messages()
    {
        var schema = Theo.Object<SignUp>()
            .Field(x => x.Password, Theo.String().MinLength(3))
            .Refine(x => x.Password != x.PasswordConfirmation, "They must differ.");

        var flat = schema.SafeParse(new SignUp { Password = "same", PasswordConfirmation = "same" })
            .Errors.Flatten();

        Assert.Equal("They must differ.", Assert.Single(flat.RootErrors));
        Assert.Empty(flat.FieldErrors);
    }

    [Fact]
    public void ToDictionary_Files_Root_Messages_Under_The_Empty_Key()
    {
        var schema = Theo.Object<SignUp>()
            .Field(x => x.Password, Theo.String().MinLength(10))
            .Refine(_ => false, "Always fails.");

        var flat = schema.SafeParse(new SignUp { Password = "short" }).Errors.Flatten();
        var dictionary = flat.ToDictionary();

        // The field failed, so the object rule never ran; only the field key is present.
        Assert.Equal(new[] { "Password" }, dictionary.Keys.ToArray());
    }

    [Fact]
    public void ToDictionary_Accepts_A_Custom_Root_Key()
    {
        var schema = Theo.Object<SignUp>().Refine(_ => false, "Nope.");

        var dictionary = schema.SafeParse(new SignUp()).Errors.Flatten().ToDictionary("$");

        Assert.Equal("Nope.", Assert.Single(dictionary["$"]));
    }

    [Fact]
    public void Flatten_Of_Nothing_Is_Empty()
    {
        var flat = UserSchema.SafeParse(new CreateUserRequest
        {
            Name = "Ada",
            Email = "ada@example.com",
        }).Errors.Flatten();

        Assert.True(flat.IsEmpty);
        Assert.Empty(flat.ToDictionary());
    }

    [Fact]
    public void Flatten_Keys_Collection_Elements_By_Rendered_Path()
    {
        var schema = Theo.Object<Mailing>()
            .Field(x => x.Recipients, Theo.Collection(Theo.String().Email()));

        var flat = schema.SafeParse(new Mailing { Recipients = ["ok@example.com", "bad", "worse"] })
            .Errors.Flatten();

        Assert.Equal(new[] { "Recipients[1]", "Recipients[2]" }, flat.FieldErrors.Keys.ToArray());
    }

    [Fact]
    public void ToTree_Mirrors_The_Shape_Of_The_Value()
    {
        var errors = ErrorsFor(new CreateUserRequest
        {
            Name = "ab",
            Email = "nope",
            Address = new Address { Street = "x", ZipCode = "123" },
        });

        var tree = errors.ToTree();

        Assert.Empty(tree.Errors);
        Assert.Equal("Invalid e-mail address.", Assert.Single(tree.Properties["Email"].Errors));

        var address = tree.Properties["Address"];
        Assert.Empty(address.Errors);
        Assert.Equal(2, address.Properties.Count);
        Assert.Single(address.Properties["ZipCode"].Errors);
    }

    [Fact]
    public void ToTree_Keys_Elements_By_Index_And_Stays_Sparse()
    {
        var schema = Theo.Object<Mailing>()
            .Field(x => x.Recipients, Theo.Collection(Theo.String().Email()));

        var value = new Mailing
        {
            Recipients = [.. Enumerable.Range(0, 50).Select(i => i == 37 ? "bad" : $"u{i}@example.com")],
        };

        var tree = schema.SafeParse(value).Errors.ToTree();
        var recipients = tree.Properties["Recipients"];

        // One bad row in fifty costs one entry, not fifty.
        Assert.Single(recipients.Items);
        Assert.True(recipients.Items.ContainsKey(37));
        Assert.Single(recipients.Items[37].Errors);
    }

    [Fact]
    public void ToTree_Puts_Object_Level_Messages_At_The_Root()
    {
        var schema = Theo.Object<SignUp>().Refine(_ => false, "Object rule failed.");

        var tree = schema.SafeParse(new SignUp()).Errors.ToTree();

        Assert.Equal("Object rule failed.", Assert.Single(tree.Errors));
        Assert.Empty(tree.Properties);
    }

    [Fact]
    public void ToTree_Of_Nothing_Is_Empty() =>
        Assert.True(Array.Empty<ValidationError>().ToTree().IsEmpty);

    [Fact]
    public async Task Formatting_Works_On_Async_Results_Too()
    {
        var schema = Theo.Object<CreateUserRequest>()
            .Field(x => x.Email, Theo.String().Email()
                .RefineAsync((_, _) => ValueTask.FromResult(false), "Already registered."));

        var result = await schema.SafeParseAsync(
            new CreateUserRequest { Email = "ada@example.com" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Already registered.", Assert.Single(result.Errors.Flatten().FieldErrors["Email"]));
    }
}
