using Theon.Errors;

namespace Theon.Tests;

public enum UserStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
}

[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
}

public enum ByteBacked : byte
{
    One = 1,
    Two = 2,
}

public enum LongBacked : long
{
    Big = 1L << 40,
}

public class EnumSchemaTests
{
    [Fact]
    public void A_Declared_Member_Passes()
    {
        var schema = Theo.Enum<UserStatus>();

        Assert.True(schema.IsValid(UserStatus.Active));
        Assert.True(schema.IsValid(UserStatus.Pending));
    }

    [Fact]
    public void A_Value_The_Type_Never_Declared_Is_Rejected()
    {
        // This is the whole point. The cast is legal C#, the compiler is happy, and the value
        // reaches your code typed as UserStatus while matching no member of it.
        var schema = Theo.Enum<UserStatus>();

        var result = schema.SafeParse((UserStatus)999);

        Assert.False(result.IsSuccess);
        Assert.Equal(ValidationErrorCode.InvalidValue, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Zero_Is_Rejected_When_The_Type_Declares_No_Zero_Member()
    {
        Assert.False(Theo.Enum<ByteBacked>().IsValid(default));
    }

    [Fact]
    public void A_Custom_Message_Replaces_The_Default()
    {
        var result = Theo.Enum<UserStatus>().WithMessage("Unknown status.").SafeParse((UserStatus)42);

        Assert.Equal("Unknown status.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void Refine_Narrows_An_Enum_To_A_Subset()
    {
        var schema = Theo.Enum<UserStatus>()
            .Refine(static s => s != UserStatus.Suspended, "A suspended user cannot be created.");

        Assert.True(schema.IsValid(UserStatus.Active));
        Assert.False(schema.IsValid(UserStatus.Suspended));
    }

    [Fact]
    public void AllowNull_Accepts_Null()
    {
        var schema = Theo.Enum<UserStatus>().AllowNull();

        Assert.True(schema.SafeParse(null).IsSuccess);
        Assert.True(schema.SafeParse(UserStatus.Active).IsSuccess);
        Assert.False(schema.SafeParse((UserStatus)77).IsSuccess);
    }

    [Fact]
    public void Enums_Backed_By_Other_Integral_Types_Work()
    {
        Assert.True(Theo.Enum<ByteBacked>().IsValid(ByteBacked.Two));
        Assert.False(Theo.Enum<ByteBacked>().IsValid((ByteBacked)9));

        Assert.True(Theo.Enum<LongBacked>().IsValid(LongBacked.Big));
        Assert.False(Theo.Enum<LongBacked>().IsValid((LongBacked)7));
    }

    [Fact]
    public void A_Flags_Combination_Is_Accepted_Even_Though_No_Member_Matches_It()
    {
        // Read | Write is 3, which is not a declared member. Rejecting it would be wrong.
        var schema = Theo.Enum<Permissions>();

        Assert.True(schema.IsValid(Permissions.Read | Permissions.Write));
        Assert.True(schema.IsValid(Permissions.Read | Permissions.Write | Permissions.Delete));
        Assert.True(schema.IsValid(Permissions.None));
    }

    [Fact]
    public void A_Flags_Value_With_An_Undeclared_Bit_Is_Rejected()
    {
        var schema = Theo.Enum<Permissions>();

        // 8 is not a declared flag, so neither 8 nor 9 (Read | 8) is a legitimate combination.
        Assert.False(schema.IsValid((Permissions)8));
        Assert.False(schema.IsValid(Permissions.Read | (Permissions)8));
    }

    [Fact]
    public void A_Non_Flags_Enum_Does_Not_Accept_Combinations()
    {
        // Active | Suspended is 3, which UserStatus does not declare and, lacking [Flags],
        // does not mean anything either.
        Assert.False(Theo.Enum<UserStatus>().IsValid(UserStatus.Active | UserStatus.Suspended));
    }
}
