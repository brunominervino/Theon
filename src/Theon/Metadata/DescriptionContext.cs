using System.Globalization;

namespace Theon.Metadata;

/// <summary>Carries one description in progress, and breaks any cycle in it.</summary>
/// <remarks>
/// <para>
/// A schema may refer to itself, so a walk that followed every child blindly would never return. The
/// cure is the same one a document uses: describe a repeating schema once, give it a name, and point at
/// that name from everywhere it appears. That is why a composite schema describes its children through
/// <see cref="Describe"/> rather than by calling them itself.
/// </para>
/// <para>
/// Two members, and they are the whole contract. Everything else about a description in progress — what
/// the definitions are called, how deep the walk has gone, which schemas it is already inside — is this
/// library's business and may change. A context is not constructed by callers: ask a schema to describe
/// itself with <c>Describe</c> and one is made for you.
/// </para>
/// </remarks>
public sealed class DescriptionContext
{
    // Deep enough for any hand-written schema, and shallow enough to fail before the stack does.
    // The same reasoning as ParseOptions.MaxDepth, for the same reason.
    private const int MaxDepth = 64;

    private readonly Dictionary<object, string> _names = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _inProgress = new(ReferenceEqualityComparer.Instance);
    private readonly string _referencePrefix;
    private int _depth;

    internal DescriptionContext(string referencePrefix, DescriptionDirection direction)
    {
        _referencePrefix = referencePrefix;
        Direction = direction;
    }

    /// <summary>Gets which side of each schema is being described.</summary>
    /// <remarks>
    /// Carried here rather than passed as an argument, because it has to reach every descendant: a
    /// direction that stopped at the outermost schema would describe the outside of an object and the
    /// inside of nothing. A composite schema that describes its children through
    /// <see cref="Describe"/> propagates it without having to know it exists.
    /// </remarks>
    public DescriptionDirection Direction { get; }

    // Not part of the contract. Exposing it would fix $defs as a dictionary for ever, and a schema
    // describing its children has no need of it.
    internal Dictionary<string, SchemaDescription> Definitions { get; } = [];

    /// <summary>Describes one schema, breaking any cycle it is part of.</summary>
    /// <typeparam name="TInput">The type the schema accepts.</typeparam>
    /// <typeparam name="TOutput">The type the schema produces.</typeparam>
    /// <param name="schema">The schema to describe.</param>
    /// <returns>
    /// The description, or a reference to it when the schema is one this walk is already inside.
    /// </returns>
    /// <remarks>
    /// This is how a composite schema describes a child. Calling the child's <c>Describe</c> directly
    /// would skip the cycle-breaking and run until the stack ended on any schema that contains itself.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The walk went deeper than the generator will follow, which a recursive schema built from a
    /// factory that returns a new instance each time will do.
    /// </exception>
    public SchemaDescription Describe<TInput, TOutput>(Schema<TInput, TOutput> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (_inProgress.Contains(schema))
        {
            // We are already inside this schema, so it refers to itself. Name it and point at the
            // name; the description being built further up the stack will be moved into the
            // definitions when it finishes.
            return new SchemaDescription { Reference = _referencePrefix + NameFor<TOutput>(schema) };
        }

        if (_depth >= MaxDepth)
        {
            throw new InvalidOperationException(
                $"Describing this schema went deeper than {MaxDepth} levels. A recursive schema is " +
                "usually the cause: have the factory passed to Theo.Lazy hand back a schema that " +
                "already exists, such as a static field, rather than build a new one on every call. " +
                "A factory that builds a fresh schema each time produces a new instance at every " +
                "level, so there is no repetition for this to recognise.");
        }

        _inProgress.Add(schema);
        _depth++;

        SchemaDescription described;
        try
        {
            described = schema.Describe(this);
        }
        finally
        {
            _depth--;
            _inProgress.Remove(schema);
        }

        // Something below us pointed back at this schema while we were describing it, so the name it
        // was given now has to resolve to something.
        if (_names.TryGetValue(schema, out var name))
        {
            Definitions[name] = described;
            return new SchemaDescription { Reference = _referencePrefix + name };
        }

        return described;
    }

    private string NameFor<TOutput>(object schema)
    {
        if (_names.TryGetValue(schema, out var existing))
        {
            return existing;
        }

        // Named after the type it validates, which is what a reader of the document is looking for.
        // A suffix is only added when two different schemas describe the same type, which happens
        // when one type has more than one schema in the same document.
        var baseName = typeof(TOutput).Name;
        var name = baseName;
        var suffix = 2;

        while (_names.ContainsValue(name))
        {
            name = baseName + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        _names[schema] = name;
        return name;
    }
}
