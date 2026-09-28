namespace Theon.Tests;

public class CrossFieldTests
{
    private static readonly Schema<SignUp> SignUpSchema =
        Theo.Object<SignUp>()
            .Field(x => x.Password, Theo.String().MinLength(8))
            .Field(x => x.PasswordConfirmation, Theo.String())
            .Refine(
                x => x.Password == x.PasswordConfirmation,
                x => x.PasswordConfirmation,
                "The passwords do not match.");

    [Fact]
    public void Matching_Passwords_Pass()
    {
        var value = new SignUp { Password = "correct horse", PasswordConfirmation = "correct horse" };

        Assert.True(SignUpSchema.SafeParse(value).IsSuccess);
    }

    [Fact]
    public void The_Error_Lands_On_The_Named_Property()
    {
        var value = new SignUp { Password = "correct horse", PasswordConfirmation = "battery staple" };

        var result = SignUpSchema.SafeParse(value);

        var error = Assert.Single(result.Errors);
        Assert.Equal("PasswordConfirmation", error.Path.ToString());
        Assert.Equal("The passwords do not match.", error.Message);
    }

    [Fact]
    public void Object_Rules_Do_Not_Run_While_A_Field_Is_Still_Invalid()
    {
        // Password is too short, so only that is reported. Adding "and they do not match" on top
        // would be noise, and a cross-field rule written against valid values should never have to
        // defend itself against invalid ones.
        var value = new SignUp { Password = "short", PasswordConfirmation = "different" };

        var result = SignUpSchema.SafeParse(value);

        var error = Assert.Single(result.Errors);
        Assert.Equal("Password", error.Path.ToString());
    }

    [Fact]
    public void An_Object_Rule_Without_A_Path_Reports_At_The_Root()
    {
        var schema = Theo.Object<SignUp>()
            .Refine(x => x.Password != x.PasswordConfirmation, "They must differ.");

        var value = new SignUp { Password = "same", PasswordConfirmation = "same" };

        var error = Assert.Single(schema.SafeParse(value).Errors);
        Assert.True(error.Path.IsRoot);
        Assert.Equal("They must differ.", error.ToString());
    }
}
