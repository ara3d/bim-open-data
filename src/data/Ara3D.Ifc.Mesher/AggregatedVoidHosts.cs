using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.Models;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Geometry for elements that declare openings (<c>IFCRELVOIDSELEMENT</c> hosts) but carry no
/// representation of their own: the host solid is derived from their <c>IFCRELAGGREGATES</c>
/// children and the host's openings are carved from it. This mirrors web-ifc, which must materialise
/// such a host's solid (from its aggregated parts) before it can subtract the voids, as for the
/// duplex flat roof <c>#22475</c>, whose geometry is the aggregated slab <c>#22492</c>. Aggregate
/// parents that are not void hosts (stairs, and spatial containers such as building, storey, and
/// site) are left to emit their children individually, matching the oracle.
/// </summary>
internal static class AggregatedVoidHosts
{
    public static void Emit(
        MeshingContext ctx,
        Model3DBuilder builder,
        Dictionary<long, List<int>> meshBuckets,
        Dictionary<int, List<int>> voidRelations,
        Dictionary<int, List<TriangleMesh3D>> openingSolidCache)
    {
        foreach (var (parentId, childIds) in CollectAggregateChildren(ctx))
        {
            if (!voidRelations.TryGetValue(parentId, out var openingIds))
                continue;
            var parent = ctx.GetEntityOrDefault(parentId);
            if (parent is null || ModelAssembler.IsProduct(parent))
                continue; // products with their own representation are emitted by BuildModel

            ctx.Try(() =>
            {
                var worldMeshes = CollectAggregatedChildMeshes(ctx, childIds);
                if (worldMeshes.Count == 0)
                    return;

                var mesh = MeshHelpers.Merge(worldMeshes);
                var worldPrisms = new List<TriangleMesh3D>();
                foreach (var openingId in openingIds)
                {
                    if (!openingSolidCache.TryGetValue(openingId, out var solids))
                        openingSolidCache[openingId] = solids = OpeningCarver.BuildOpeningWorldSolids(ctx, openingId);
                    worldPrisms.AddRange(solids);
                }
                mesh = OpeningCarver.CarveConvex(mesh, worldPrisms);
                if (mesh.FaceIndices.Count == 0)
                    return;

                // The child meshes are baked into world coordinates, so the instance transform is
                // identity (matching how the oracle stores these attributed-up meshes).
                var meshIdx = MeshDedup.GetOrAddMesh(builder, meshBuckets, parentId, mesh);
                builder.AddInstance(meshIdx, Matrix4x4.Identity, Material.Default, parentId);
                ctx.Diagnostics.RecordApproximate("IFCRELAGGREGATES",
                    $"Aggregated void-host geometry attributed to #{parentId}");
            }, parent.GetEntityName(), $"aggregated void host #{parentId}");
        }
    }

    /// <summary>Builds the world-space meshes of an aggregate's children (each placed by its own placement).</summary>
    static List<TriangleMesh3D> CollectAggregatedChildMeshes(MeshingContext ctx, IReadOnlyList<int> childIds)
    {
        var worldMeshes = new List<TriangleMesh3D>();
        foreach (var childId in childIds)
        {
            var child = ctx.GetEntityOrDefault(childId);
            var childRep = child is null ? null : MeshHelpers.ResolveOptional(ctx, child, IfcProduct.Instance.Representation);
            if (childRep is null)
                continue;

            ctx.CurrentProductId = childId;
            var childParts = new List<CollectedPart>();
            GeometryPartCollector.CollectParts(ctx, childRep, Matrix4x4.Identity, childId, childParts);
            if (childParts.Count == 0)
                continue;

            var placement = MeshHelpers.ResolveOptional(ctx, child!, IfcProduct.Instance.ObjectPlacement);
            var childWorld = placement is null
                ? Matrix4x4.Identity
                : Placements.ReadLocalPlacement(ctx, placement).Matrix;

            foreach (var part in childParts)
                worldMeshes.Add(MeshHelpers.Transform(part.Mesh, part.Transform * childWorld));
        }
        return worldMeshes;
    }

    /// <summary>relating (parent) express id -&gt; aggregated child express ids (IFCRELAGGREGATES).</summary>
    static Dictionary<int, List<int>> CollectAggregateChildren(MeshingContext ctx)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var e in ctx.Resolver.GetEntities())
        {
            if (e.GetEntityName() != "IFCRELAGGREGATES")
                continue;
            var parentId = MeshHelpers.ReadOptionalId(e, IfcRelAggregates.Instance.RelatingObject);
            if (parentId is null)
                continue;
            var kids = MeshHelpers.ReadIds(e, IfcRelAggregates.Instance.RelatedObjects);
            if (kids.Count == 0)
                continue;
            if (!map.TryGetValue(parentId.Value, out var list))
                map[parentId.Value] = list = new List<int>();
            list.AddRange(kids);
        }
        return map;
    }
}
