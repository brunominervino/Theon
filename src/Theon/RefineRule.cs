namespace Theon;

/// <summary>
/// A caller-supplied rule that reports its own failures, rather than answering yes or no.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <param name="value">The value to examine. Has already satisfied everything before this rule.</param>
/// <param name="context">
/// The parse in progress. Call <see cref="ParseContext.AddError"/> as many times as there are things
/// wrong, and <see cref="ParseContext.PushProperty"/> to report against a particular property.
/// </param>
/// <remarks>
/// <para>
/// A delegate of its own rather than an <c>Action</c>, because <see cref="ParseContext"/> is a
/// <see langword="ref struct"/> and so cannot be a type argument on the frameworks this library
/// targets. Declaring the parameter <see langword="ref"/> on a delegate of our own works on both.
/// </para>
/// <para>
/// A rule that reports nothing has accepted the value.
/// </para>
/// </remarks>
public delegate void RefineRule<in T>(T value, ref ParseContext context);
