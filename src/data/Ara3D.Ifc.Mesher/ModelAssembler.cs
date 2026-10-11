using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.Models;

namespace Ara3D.Ifc.Mesher;

/// <summary>Traverses IFC products and assembles a <see cref="Model3D"/> with per-part instancing.
/// Identical meshes are shared through <see cref="MeshDedup"/>; void hosts without their own
/// representation are handled by <see cref="AggregatedVoidHosts"/>.</summary>
public static class ModelAssembler
{
    public static (Model3D Model, MeshingDiagnostics Diagnostics) BuildModel(IfcEntityResolver resolver)
    {
        var ctx = new MeshingContext(resolver);
        var builder = new Model3DBuilder();
        var meshBuckets = new Dictionary<long, List<int>>();

        var voidRelations = OpeningCarver.CollectVoidRelations(ctx);
        var openingSolidCache = new Dictionary<int, List<TriangleMesh3D>>();

        foreach (var entity in ctx.Resolver.GetEntities())
        {
            if (!IsProduct(entity))
                continue;

            ctx.CurrentProductId = entity.Id;
            ctx.Try(() =>
            {
                var representation = MeshHelpers.ResolveOptional(ctx, entity, IfcProduct.Instance.Representation);
                if (representation is null)
                    return;

                var placement = MeshHelpers.ResolveOptional(ctx, entity, IfcProduct.Instance.ObjectPlacement);
                var productMatrix = placement is null
                    ? Matrix4x4.Identity
                    : Placements.ReadLocalPlacement(ctx, placement).Matrix;

                var parts = new List<ScopedPart>();
                CollectScopedParts(ctx, representation, Matrix4x4.Identity, entity.Id, parts);
                if (parts.Count == 0)
                    return;

                if (voidRelations.TryGetValue(entity.Id, out var openingIds))
                    parts = CarveOpenings(ctx, parts, openingIds, productMatrix, openingSolidCache);

                foreach (var part in parts)
                {
                    var meshIdx = MeshDedup.GetOrAddMesh(builder, meshBuckets, part.DedupScope, part.Part.Mesh);
                    // Row-vector convention (System.Numerics): world = local * part * product.
                    var matrix = part.Part.Transform * productMatrix;
                    builder.AddInstance(meshIdx, matrix, part.Part.Material, part.Part.EntityIndex);
                }
            }, entity.GetEntityName(), $"product #{entity.Id}");
        }

        ctx.CurrentProductId = null;
        AggregatedVoidHosts.Emit(ctx, builder, meshBuckets, voidRelations, openingSolidCache);
        RecordOpeningRelations(ctx);
        return (builder.Build(), ctx.Diagnostics);
    }

    static List<ScopedPart> CarveOpenings(
        MeshingContext ctx,
        List<ScopedPart> parts,
        List<int> openingIds,
        Matrix4x4 productMatrix,
        Dictionary<int, List<TriangleMesh3D>> openingSolidCache)
    {
        var worldSolids = new List<TriangleMesh3D>();
        foreach (var openingId in openingIds)
        {
            if (!openingSolidCache.TryGetValue(openingId, out var solids))
            {
                solids = OpeningCarver.BuildOpeningWorldSolids(ctx, openingId);
                openingSolidCache[openingId] = solids;
            }
            worldSolids.AddRange(solids);
        }
        if (worldSolids.Count == 0)
            return parts;

        var result = new List<ScopedPart>(parts.Count);
        foreach (var part in parts)
        {
            var toLocal = (part.Part.Transform * productMatrix).Invert;
            var mesh = part.Part.Mesh;
            ctx.Try(() =>
            {
                var localPrisms = worldSolids.Select(w => MeshHelpers.Transform(w, toLocal)).ToList();
                mesh = OpeningCarver.CarveConvex(mesh, localPrisms);
            }, "IFCRELVOIDSELEMENT", $"carve product #{part.Part.EntityIndex}");
            result.Add(part with { Part = new CollectedPart(mesh, part.Part.Transform, part.Part.EntityIndex, part.Part.Material) });
        }
        return result;
    }

    readonly record struct ScopedPart(CollectedPart Part, int DedupScope);

    /// <summary>
    /// Like <see cref="GeometryPartCollector.CollectParts"/> but tags each part with a dedup scope
    /// (representation-map or shape-representation express id) so identical bolt caps from different
    /// type maps stay separate, matching web-ifc mesh granularity.
    /// </summary>
    static void CollectScopedParts(
        MeshingContext ctx,
        IfcEntity entity,
        Matrix4x4 parentTransform,
        int productEntityId,
        List<ScopedPart> parts,
        int dedupScope = 0,
        Material? material = null)
    {
        switch (entity.GetEntityName())
        {
            case "IFCPRODUCTDEFINITIONSHAPE":
                foreach (var repId in MeshHelpers.ReadIds(entity, IfcProductRepresentation.Instance.Representations))
                    CollectScopedParts(ctx, ctx.GetEntity(repId), parentTransform, productEntityId, parts, dedupScope, material);
                return;

            case "IFCSHAPEREPRESENTATION" or "IFCREPRESENTATION":
                if (!IsBodyRepresentation(entity))
                    return;

                foreach (var itemId in MeshHelpers.ReadIds(entity, IfcRepresentation.Instance.Items))
                    CollectScopedParts(ctx, ctx.GetEntity(itemId), parentTransform, productEntityId, parts, entity.Id, material);
                return;

            case "IFCMAPPEDITEM":
                var map = MeshHelpers.ResolveRequired(ctx, entity, IfcMappedItem.Instance.MappingSource);
                var rep = MeshHelpers.ResolveRequired(ctx, map, IfcRepresentationMap.Instance.MappedRepresentation);
                if (!GeometryDispatcher.TryGetMappedItemTransform(ctx, entity, out var mappingTransform))
                    return;
                // Row-vector: apply mapping first, then parent (nested maps = inner * outer).
                CollectScopedParts(ctx, rep, mappingTransform * parentTransform, productEntityId, parts, map.Id, material);
                return;

            case "IFCFACEBASEDSURFACEMODEL":
                ctx.Diagnostics.RecordSupported("IFCFACEBASEDSURFACEMODEL");
                var faceMat = material ?? ctx.TryGetItemMaterial(entity.Id) ?? Material.Default;
                foreach (var faceId in MeshHelpers.ReadIds(entity, IfcFaceBasedSurfaceModel.Instance.FbsmFaces))
                {
                    var mesh = Brep.BuildFaceBasedSurfaceElement(ctx, ctx.GetEntity(faceId));
                    if (mesh.FaceIndices.Count > 0)
                        parts.Add(new ScopedPart(new CollectedPart(mesh, parentTransform, productEntityId, faceMat), dedupScope));
                }
                return;

            case "IFCSTYLEDITEM":
                var resolved = StyleResolver.TryResolveMaterial(ctx, entity) ?? material;
                var styledItem = MeshHelpers.ResolveOptional(ctx, entity, IfcStyledItem.Instance.Item);
                if (styledItem is not null)
                    CollectScopedParts(ctx, styledItem, parentTransform, productEntityId, parts, dedupScope, resolved);
                return;

            default:
                var built = GeometryDispatcher.TryBuild(ctx, entity);
                if (built is not null && built.Value.FaceIndices.Count > 0)
                {
                    var mat = material ?? ctx.TryGetItemMaterial(entity.Id) ?? Material.Default;
                    parts.Add(new ScopedPart(new CollectedPart(built.Value, parentTransform, productEntityId, mat), dedupScope));
                }
                return;
        }
    }

    static bool IsBodyRepresentation(IfcEntity representation)
    {
        static bool Matches(string? value) =>
            !string.IsNullOrEmpty(value) &&
            (value.Contains("Body", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("SweptSolid", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Tessellation", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Brep", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Facetation", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Clipping", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("SurfaceModel", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("MappedRepresentation", StringComparison.OrdinalIgnoreCase));

        var identifier = representation.GetString(IfcRepresentation.Instance.RepresentationIdentifier.Index);
        if (Matches(identifier))
            return true;

        var repType = representation.GetString(IfcRepresentation.Instance.RepresentationType.Index);
        return Matches(repType);
    }

    internal static bool IsProduct(IfcEntity entity)
    {
        var name = entity.GetEntityName();
        if (name.StartsWith("IFCREL", StringComparison.Ordinal) ||
            name is "IFCOWNERHISTORY" or "IFCPROJECT" or "IFCGEOMETRICREPRESENTATIONCONTEXT" or "IFCGEOMETRICREPRESENTATIONSUBCONTEXT" or
            "IFCOPENINGELEMENT")
            return false;

        if (entity.Attributes.Count <= IfcProduct.Instance.Representation.Index)
            return false;
        if (!entity.GetValue(IfcProduct.Instance.Representation.Index).IsId)
            return false;
        return true;
    }

    static void RecordOpeningRelations(MeshingContext ctx)
    {
        foreach (var entity in ctx.Resolver.GetEntities())
        {
            if (entity.GetEntityName() == "IFCRELVOIDSELEMENT")
                ctx.Diagnostics.RecordApproximate("IFCRELVOIDSELEMENT", "Opening subtracted via convex carve");
        }
    }

    public static TriangleMesh3D? BuildEntityMesh(MeshingContext ctx, IfcEntity entity)
    {
        if (IsProduct(entity))
        {
            ctx.CurrentProductId = entity.Id;
            var representation = MeshHelpers.ResolveOptional(ctx, entity, IfcProduct.Instance.Representation);
            if (representation is null)
                return null;

            var parts = new List<CollectedPart>();
            GeometryPartCollector.CollectParts(ctx, representation, Matrix4x4.Identity, entity.Id, parts);
            if (parts.Count == 0)
                return null;

            var meshes = parts
                .Select(p => MeshHelpers.Transform(p.Mesh, p.Transform))
                .ToList();
            var mesh = meshes.Count == 1 ? meshes[0] : MeshHelpers.Merge(meshes);

            var placement = MeshHelpers.ResolveOptional(ctx, entity, IfcProduct.Instance.ObjectPlacement);
            return placement is null
                ? mesh
                : MeshHelpers.Transform(mesh, Placements.ReadLocalPlacement(ctx, placement).Matrix);
        }
        return GeometryDispatcher.TryBuild(ctx, entity);
    }
}
