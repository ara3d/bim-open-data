namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>A parameter with its descriptor resolved, for assertions.</summary>
public sealed record ParameterValue(string Group, string Name, ParameterType Type, object Value);

/// <summary>Reads BOS tables by name instead of by index, so a test states what it checks.</summary>
public static class BimDataQueries
{
    public static string GlobalId(this BimData d, int entity)
        => d.Strings[(int)d.Entities[entity].GlobalId];

    public static string Name(this BimData d, int entity)
        => d.Strings[(int)d.Entities[entity].Name];

    /// <summary>The IFC class of an entity: the name of its category entity, or "" for a category.</summary>
    public static string Category(this BimData d, int entity)
        => d.Entities[entity].Category is var c && (int)c >= 0 ? d.Name((int)c) : "";

    public static int EntityByGlobalId(this BimData d, string globalId)
        => Enumerable.Range(0, d.Entities.Length).Single(i => d.GlobalId(i) == globalId);

    public static int EntityByLocalId(this BimData d, long localId)
        => Enumerable.Range(0, d.Entities.Length).Single(i => d.Entities[i].LocalId == localId);

    public static IEnumerable<ParameterValue> ParametersOf(this BimData d, int entity)
        => d.Parameters.Where(p => (int)p.Entity == entity).Select(p =>
        {
            var desc = d.Descriptors[(int)p.Descriptor];
            object value = desc.Type switch
            {
                ParameterType.String => d.Strings[p.Value],
                ParameterType.Number => d.Numbers[p.Value],
                ParameterType.Entity => p.Value,
                ParameterType.Point => d.Points[p.Value],
                _ => p.Value,
            };
            return new ParameterValue(d.Strings[(int)desc.Group], d.Strings[(int)desc.Name], desc.Type, value);
        });

    /// <summary>Relations of one type as (GlobalId of A, GlobalId of B) pairs, skipping any whose
    /// ends have no GlobalId.</summary>
    public static HashSet<(string A, string B)> RelationPairs(this BimData d, RelationType type)
        => d.Relations
            .Where(r => r.RelationType == type)
            .Select(r => (d.GlobalId((int)r.EntityA), d.GlobalId((int)r.EntityB)))
            .Where(p => p.Item1 != "" && p.Item2 != "")
            .ToHashSet();

    public static Dictionary<string, int> CountByCategory(this BimData d)
        => Enumerable.Range(0, d.Entities.Length)
            .Select(d.Category)
            .Where(c => c != "")
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
}
