using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using Theon.Checks;
using Theon.Errors;
using Theon.Metadata;

namespace Theon.Schemas;

/// <summary>
/// Validates that an enum value is one the type actually declares.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
/// <remarks>
/// <para>
/// This looks redundant and is not. C# lets you cast any number to an enum:
/// <c>(UserStatus)999</c> compiles, runs, and produces a value of that type that matches no member.
/// Deserializers reach enums the same way, from whatever number was in the payload. The type system
/// checks the shape of an enum but never its contents, so this is the one place where a validation
/// library is doing work the compiler genuinely cannot.
/// </para>
/// <para>
/// Enums marked <see cref="FlagsAttribute"/> are treated differently, because for them a
/// combination such as <c>Read | Write</c> is legitimate even though it matches no single member.
/// Those are validated by their bits: every bit set must belong to some declared member.
/// </para>
/// </remarks>
public sealed class EnumSchema<TEnum> : Schema<TEnum>
    where TEnum : struct, Enum
{
    // Static generic fields are initialised once per TEnum, so the reflection below runs at most
    // once per enum type in the process, never during a parse.
    private static readonly bool IsFlags =
        typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

    private static readonly FrozenSet<TEnum> DeclaredValues =
        Enum.GetValues<TEnum>().ToFrozenSet();

    private static readonly ulong DeclaredBits = ComputeDeclaredBits();

    private static readonly string[] MemberNames = Enum.GetNames<TEnum>();

    // The name each member serializes under, where it said so, and null where it did not. Read once
    // per enum type in the process, in a static initializer, never during a parse -- the same bargain
    // the IsFlags lookup above already makes.
    private static readonly string?[] DeclaredNames = ReadDeclaredNames();

    private readonly Check<TEnum>[] _checks;
    private readonly string? _message;

    internal EnumSchema()
        : this([], null)
    {
    }

    private EnumSchema(Check<TEnum>[] checks, string? message)
    {
        _checks = checks;
        _message = message;
    }

    /// <summary>Replaces the message reported when the value matches no declared member.</summary>
    /// <param name="message">The message to report.</param>
    public EnumSchema<TEnum> WithMessage(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new EnumSchema<TEnum>(_checks, message);
    }

    /// <summary>Requires the value to satisfy an arbitrary predicate.</summary>
    /// <param name="predicate">Returns <see langword="true"/> when the value is acceptable.</param>
    /// <param name="message">The message to report when it is not.</param>
    /// <remarks>Use this to narrow an enum to a subset, such as the statuses a request may set.</remarks>
    public EnumSchema<TEnum> Refine(Func<TEnum, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrEmpty(message);
        return new EnumSchema<TEnum>([.. _checks, new RefineCheck<TEnum>(predicate, message)], _message);
    }

    /// <summary>Accepts <see langword="null"/> in addition to everything this schema accepts.</summary>
    public Schema<TEnum?> AllowNull() => new NullableValueSchema<TEnum>(this);

    // Described as a string with an enumeration of member names, which assumes the value travels as
    // its name. That is what a documented API does, and what JsonStringEnumConverter produces; a
    // number on the wire would want the numeric values here instead. Naming the assumption beats
    // having the document be silently right half the time.
    //
    // A flags enum gets no enumeration at all: the acceptable values are every combination of the
    // declared bits, which is not a list worth writing down and is not what "enum" means in the
    // dialect.
    /// <inheritdoc />
    public override SchemaDescription Describe(DescriptionContext context)
    {
        var description = CheckDescription.Build(context, SchemaKind.String, _checks);

        if (!IsFlags)
        {
            description.AllowedValues = SerializedNames(context.EnumMemberNamingPolicy);
        }

        return description.ToDescription();
    }

    /// <inheritdoc />
    public override bool TryParse(ref ParseContext context, TEnum input, out TEnum output)
    {
        var errorsBefore = context.ErrorCount;
        output = input;

        if (!IsDeclared(input))
        {
            context.AddError(
                new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.InvalidValue,
                    Expected = typeof(TEnum).Name,
                },
                _message);

            return false;
        }

        foreach (var check in _checks)
        {
            if (context.ShouldStop)
            {
                break;
            }

            check.Run(ref context, ref output);
        }

        return context.ErrorCount == errorsBefore;
    }

    // The list a document publishes, which has to be the list the application serializes. A member
    // that named itself keeps that name: somebody who wrote a name down meant that name and not a
    // transformation of it, which is how JsonStringEnumConverter treats it too.
    private static List<object?> SerializedNames(System.Text.Json.JsonNamingPolicy? policy)
    {
        var names = new List<object?>(MemberNames.Length);

        for (var i = 0; i < MemberNames.Length; i++)
        {
            names.Add(DeclaredNames[i] ?? policy?.ConvertName(MemberNames[i]) ?? MemberNames[i]);
        }

        return names;
    }

    // Matched by the attribute's type name rather than by a reference to it.
    // JsonStringEnumMemberNameAttribute arrived in .NET 9 and this library also targets net8.0, where
    // the type does not exist -- and there is not one #if in src, which is a property worth keeping.
    // Reading the name instead costs one string comparison per member, once per enum type, and works
    // on both targets without the conditional code decision 6 warns about.
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicFields'",
        Justification =
            "The fields being read are the members of an enum, which the trimmer does not remove: " +
            "an enum's fields are its values, and a type kept at all is kept with them. Annotating " +
            "TEnum instead would push the requirement onto Theo.Enum and from there onto every " +
            "caller, for a guarantee the runtime already makes. " +
            "The failure mode if that were ever untrue is a member whose attribute was not found, " +
            "which falls back to the C# name -- the behaviour before this method existed -- rather " +
            "than to an exception or a wrong name.")]
    private static string?[] ReadDeclaredNames()
    {
        var declared = new string?[MemberNames.Length];

        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var index = Array.IndexOf(MemberNames, field.Name);

            if (index < 0)
            {
                continue;
            }

            foreach (var attribute in field.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName
                        is not "System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute"
                    || attribute.ConstructorArguments.Count != 1
                    || attribute.ConstructorArguments[0].Value is not string name)
                {
                    continue;
                }

                declared[index] = name;
            }
        }

        return declared;
    }

    private static bool IsDeclared(TEnum value) =>
        IsFlags ? (ToBits(value) & ~DeclaredBits) == 0 : DeclaredValues.Contains(value);

    private static ulong ComputeDeclaredBits()
    {
        var bits = 0UL;
        foreach (var value in Enum.GetValues<TEnum>())
        {
            bits |= ToBits(value);
        }

        return bits;
    }

    // Reads the enum's bits without boxing. The size is a compile-time constant for any given
    // TEnum, so the JIT folds this switch away entirely.
    private static ulong ToBits(TEnum value) => Unsafe.SizeOf<TEnum>() switch
    {
        1 => Unsafe.As<TEnum, byte>(ref value),
        2 => Unsafe.As<TEnum, ushort>(ref value),
        4 => Unsafe.As<TEnum, uint>(ref value),
        _ => Unsafe.As<TEnum, ulong>(ref value),
    };
}
