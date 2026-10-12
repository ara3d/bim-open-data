using Ara3D.Models;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>Entity kind of each product and of its geometry items (mapped items followed to their
/// leaves), read through <see cref="OracleEntityMap"/>'s representation trees.</summary>
public sealed class DiscrepancyTableKinds
{
    readonly Dictionary<int, (string Product, string Geometry)> _kinds;

    DiscrepancyTableKinds(Dictionary<int, (string Product, string Geometry)> kinds) => _kinds = kinds;

    public string Product(int id) => _kinds.TryGetValue(id, out var k) ? k.Product : "?";
    public string Geometry(int id) => _kinds.TryGetValue(id, out var k) ? k.Geometry : "?";

    /// <param name="anyModel">Any built model; the map only needs one to skip loading the oracle.</param>
    public static DiscrepancyTableKinds Read(FilePath ifc, Model3D anyModel)
    {
        var map = OracleEntityMap.Build(ifc, anyModel);
        var kinds = new Dictionary<int, (string Product, string Geometry)>();
        foreach (var tree in map.ProductRepresentationTrees)
            kinds[tree.EntityId] = (tree.EntityName, string.Join("+", Leaves(tree).Distinct().Order()));
        return new DiscrepancyTableKinds(kinds);
    }

    static IEnumerable<string> Leaves(RepresentationTreeNode node)
    {
        if (node.Children.Count == 0 && node.Notes is "geometry-item")
            yield return node.EntityName;
        foreach (var leaf in node.Children.SelectMany(Leaves))
            yield return leaf;
    }
}
