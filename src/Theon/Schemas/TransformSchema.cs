namespace Theon.Schemas;

/// <summary>
/// Runs an inner schema and then maps its result to another type.
/// </summary>
/// <typeparam name="TInput">The type accepted by the inner schema.</typeparam>
/// <typeparam name="TIntermediate">The type produced by the inner schema.</typeparam>
/// <typeparam name="TOutput">The type produced after the mapping.</typeparam>
/// <remarks>
/// The mapping runs only when the inner schema succeeded, so it never sees a value that failed
/// validation and never has to defend against one.
/// </remarks>
internal sealed class TransformSchema<TInput, TIntermediate, TOutput>(
    Schema<TInput, TIntermediate> inner,
    Func<TIntermediate, TOutput> transform) : Schema<TInput, TOutput>
{
    public override bool TryParse(ref ParseContext context, TInput input, out TOutput output)
    {
        if (!inner.TryParse(ref context, input, out var intermediate))
        {
            output = default!;
            return false;
        }

        output = transform(intermediate);
        return true;
    }
}
