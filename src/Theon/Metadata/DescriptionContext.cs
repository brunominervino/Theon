using System.Globalization;

namespace Theon.Metadata;

// Carries the state of one description: which schemas are already being described, and the
// definitions they were promoted into.
//
// A schema may refer to itself, so a walk that followed every child blindly would never return. The
// cure is the same one a document uses: describe a repeating schema once, give it a name, and point
// at that name from everywhere it appears.
internal sealed class DescriptionContext
{
    // Deep enough for any hand-written schema, and shallow enough to fail before the stack does.
    // The same reasoning as ParseOptions.MaxDepth, for the same reason.
    private const int MaxDepth = 64;

    private readonly Dictionary<object, string> _names = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _inProgress = new(ReferenceEqualityComparer.Instance);
    private readonly string _referencePrefix;
    private int _depth;

    internal DescriptionContext(string referencePrefix) => _referencePrefix = referencePrefix;

    internal Dictionary<string, SchemaDescription> Definitions { get; } = [];

    // Describes one schema, breaking any cycle it is part of.
    internal SchemaDescription Describe<TInput, TOutput>(Schema<TInput, TOutput> schema)
    {
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
