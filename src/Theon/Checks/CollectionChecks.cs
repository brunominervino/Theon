using Theon.Errors;
using Theon.Metadata;

namespace Theon.Checks;

// The count rules are generic over the collection type, not written against IReadOnlyList, because a
// list and a set both have a count and both want the same three rules. Check<T> takes its value by
// reference and so is invariant, which is why one implementation needs the type parameter rather
// than a shared base interface.
internal sealed class MinCountCheck<TCollection, TElement>(int minimum) : Check<TCollection>
    where TCollection : IReadOnlyCollection<TElement>
{
    internal override void Describe(SchemaDescriptionBuilder description) =>
        description.MinItems = minimum;

    internal override void Run(ref ParseContext context, ref TCollection value)
    {
        if (value.Count >= minimum)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooSmall,
                Origin = ValidationOrigin.Collection,
                Minimum = minimum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class MaxCountCheck<TCollection, TElement>(int maximum) : Check<TCollection>
    where TCollection : IReadOnlyCollection<TElement>
{
    internal override void Describe(SchemaDescriptionBuilder description) =>
        description.MaxItems = maximum;

    internal override void Run(ref ParseContext context, ref TCollection value)
    {
        if (value.Count <= maximum)
        {
            return;
        }

        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.TooBig,
                Origin = ValidationOrigin.Collection,
                Maximum = maximum,
                Inclusive = true,
            },
            Message);
    }
}

internal sealed class ExactCountCheck<TCollection, TElement>(int count) : Check<TCollection>
    where TCollection : IReadOnlyCollection<TElement>
{
    internal override void Describe(SchemaDescriptionBuilder description)
    {
        description.MinItems = count;
        description.MaxItems = count;
    }

    internal override void Run(ref ParseContext context, ref TCollection value)
    {
        if (value.Count == count)
        {
            return;
        }

        context.AddError(
            value.Count < count
                ? new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooSmall,
                    Origin = ValidationOrigin.Collection,
                    Minimum = count,
                    Inclusive = true,
                }
                : new ValidationErrorInfo
                {
                    Code = ValidationErrorCode.TooBig,
                    Origin = ValidationOrigin.Collection,
                    Maximum = count,
                    Inclusive = true,
                },
            Message);
    }
}

// Requires every element of a list to be distinct, reporting at the index of each repeat rather
// than once about the list, so a form can mark the row a person has to fix.
//
// Two strategies, chosen by size, because a valid value must not allocate. The pairwise scan is
// quadratic and allocates nothing, which is the right trade for the lists this rule is actually
// asked about — tags, recipients, selected options, all of them short. Past the threshold the
// quadratic term stops being free, and one set allocated for one parse is cheaper than scanning a
// thousand elements against each other.
internal sealed class UniqueCheck<T>(int pairwiseLimit = 32) : Check<IReadOnlyList<T>>
{
    internal override void Describe(SchemaDescriptionBuilder description) =>
        description.UniqueItems = true;

    internal override void Run(ref ParseContext context, ref IReadOnlyList<T> value)
    {
        if (value.Count < 2)
        {
            return;
        }

        if (value.Count <= pairwiseLimit)
        {
            RunPairwise(ref context, value);
        }
        else
        {
            RunWithSet(ref context, value);
        }
    }

    private void RunPairwise(ref ParseContext context, IReadOnlyList<T> value)
    {
        var comparer = EqualityComparer<T>.Default;

        for (var i = 1; i < value.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (comparer.Equals(value[i], value[j]))
                {
                    Report(ref context, i);
                    break;
                }
            }
        }
    }

    private void RunWithSet(ref ParseContext context, IReadOnlyList<T> value)
    {
        var seen = new HashSet<T>(value.Count);

        for (var i = 0; i < value.Count; i++)
        {
            if (!seen.Add(value[i]))
            {
                Report(ref context, i);
            }
        }
    }

    private void Report(ref ParseContext context, int index)
    {
        context.PushIndex(index);
        context.AddError(
            new ValidationErrorInfo
            {
                Code = ValidationErrorCode.Duplicate,
                Origin = ValidationOrigin.Collection,
            },
            Message);
        context.Pop();
    }
}
