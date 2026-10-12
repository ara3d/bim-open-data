using System.Globalization;
using Ara3D.BimOpenSchema;
using Ara3D.DataTable;

namespace Ara3D.Ids;

/// <summary>A model's BOS relations as three parallel arrays sorted by source entity, so a
/// Snowdon-sized model (about a million edges) costs 17 bytes an edge and a lookup is a binary
/// search. An edge points from the related object to the relating one, as BOS stores it: a door
/// is ContainedIn its storey, a layer HasMaterial its material.</summary>
public sealed class IdsRelations
{
    private readonly long[] _from;
    private readonly long[] _to;
    private readonly RelationType[] _type;
    private readonly HashSet<RelationType> _carried;

    private IdsRelations(long[] from, long[] to, RelationType[] type)
    {
        var order = Enumerable.Range(0, from.Length).ToArray();
        Array.Sort(from.ToArray(), order);
        _from = order.Select(i => from[i]).ToArray();
        _to = order.Select(i => to[i]).ToArray();
        _type = order.Select(i => type[i]).ToArray();
        _carried = [.. type];
    }

    public int Count => _from.Length;

    public static IdsRelations FromBimData(IReadOnlyList<EntityRelation> relations)
        => new(
            relations.Select(r => (long)r.EntityA).ToArray(),
            relations.Select(r => (long)r.EntityB).ToArray(),
            relations.Select(r => r.RelationType).ToArray());

    /// <summary>From a table shaped like the <c>RelationText</c> view: <c>EntityIndexA</c>,
    /// <c>EntityIndexB</c>, and <c>RelationType</c> as a name (<c>HasMaterial</c>) or its number.
    /// Column names match case-insensitively; a row with an unreadable index or type is dropped.</summary>
    public static IdsRelations FromTable(IDataTable table)
    {
        var a = IdsTableColumns.Required(table, "EntityIndexA");
        var b = IdsTableColumns.Required(table, "EntityIndexB");
        var t = IdsTableColumns.Required(table, "RelationType");
        var from = new List<long>();
        var to = new List<long>();
        var type = new List<RelationType>();
        for (var row = 0; row < table.Rows.Count; row++)
        {
            var source = IdsTableColumns.Index(table, a, row);
            var target = IdsTableColumns.Index(table, b, row);
            if (source < 0 || target < 0 || ParseType(table[t, row]) is not { } kind)
                continue;
            from.Add(source);
            to.Add(target);
            type.Add(kind);
        }
        return new(from.ToArray(), to.ToArray(), type.ToArray());
    }

    private static RelationType? ParseType(object? cell)
        => cell switch
        {
            null or DBNull => null,
            string text => Enum.TryParse<RelationType>(text, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind : null,
            _ => Enum.IsDefined(typeof(RelationType), Convert.ToInt32(cell, CultureInfo.InvariantCulture))
                ? (RelationType)Convert.ToInt32(cell, CultureInfo.InvariantCulture)
                : null,
        };

    /// <summary>True when the model has at least one edge of the type. A model with none cannot
    /// tell "no such relation" from "its converter does not write it", so a facet that needs
    /// the type gives InfoNotAvailable rather than a Fail.</summary>
    public bool Carries(RelationType type)
        => _carried.Contains(type);

    /// <summary>The entities <paramref name="entity"/> points at through edges of the type.</summary>
    public IEnumerable<long> Targets(long entity, RelationType type)
    {
        for (var i = FirstFrom(entity); i < _from.Length && _from[i] == entity; i++)
            if (_type[i] == type)
                yield return _to[i];
    }

    private int FirstFrom(long entity)
    {
        int lo = 0, hi = _from.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_from[mid] < entity)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
