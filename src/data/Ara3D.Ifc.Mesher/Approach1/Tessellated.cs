using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.Ifc.Mesher.Approach1;

/// <summary>Direct tessellated geometry: triangulated and polygonal face sets.</summary>
public static class Tessellated
{
    public static TriangleMesh3D BuildTriangulatedFaceSet(MeshingContext ctx, IfcEntity entity)
    {
        ctx.Diagnostics.RecordSupported("IFCTRIANGULATEDFACESET");
        var coords = ReadCartesianPointList3D(ctx, entity);
        var coordId = entity.GetId(IfcTriangulatedFaceSet.Instance.Coordinates.Index);
        var faces = ReadTriangulatedFaces(ctx.GetEntity(coordId), entity, coords);
        return new TriangleMesh3D(coords, faces);
    }

    public static TriangleMesh3D BuildPolygonalFaceSet(MeshingContext ctx, IfcEntity entity)
    {
        ctx.Diagnostics.RecordSupported("IFCPOLYGONALFACESET");
        var coords = ReadCartesianPointList3D(ctx, entity);
        var faces = new List<Integer3>();
        foreach (var faceId in MeshHelpers.ReadIds(entity, IfcPolygonalFaceSet.Instance.Faces))
        {
            var face = ctx.GetEntity(faceId);
            var name = face.GetEntityName();
            if (name == "IFCINDEXEDPOLYGONALFACEWITHVOIDS")
            {
                ctx.Diagnostics.RecordSupported("IFCINDEXEDPOLYGONALFACEWITHVOIDS");
                TriangulatePolygonFaceWithVoids(coords, face, faces);
            }
            else if (name == "IFCINDEXEDPOLYGONALFACE")
            {
                ctx.Diagnostics.RecordSupported("IFCINDEXEDPOLYGONALFACE");
                TriangulatePolygonFace(coords, ReadPositiveIndices(face, IfcIndexedPolygonalFace.Instance.CoordIndex.Index), faces);
            }
        }
        return new TriangleMesh3D(coords, faces);
    }

    static List<Point3D> ReadCartesianPointList3D(MeshingContext ctx, IfcEntity faceSet)
    {
        var coordEntity = MeshHelpers.ResolveRequired(ctx, faceSet, IfcTessellatedFaceSet.Instance.Coordinates);
        var points = new List<Point3D>();
        var token = coordEntity.GetValue(IfcCartesianPointList3D.Instance.CoordList.Index);
        if (!token.IsList)
            return points;
        foreach (var item in token.AsList(coordEntity.Document))
        {
            if (!item.IsList)
                continue;
            var nums = item.AsList(coordEntity.Document).Where(t => t.IsNumber).Select(t => t.AsNumber()).ToList();
            points.Add(new Vector3(
                ctx.ScaleLength(nums.Count > 0 ? nums[0] : 0),
                ctx.ScaleLength(nums.Count > 1 ? nums[1] : 0),
                ctx.ScaleLength(nums.Count > 2 ? nums[2] : 0)));
        }
        return points;
    }

    static List<Integer3> ReadTriangulatedFaces(IfcEntity coordEntity, IfcEntity faceSet, IReadOnlyList<Point3D> coords)
    {
        var faces = new List<Integer3>();
        var token = faceSet.GetValue(IfcTriangulatedFaceSet.Instance.CoordIndex.Index);
        var indices = FlattenPositiveIndices(faceSet.Document, token).Select(i => i - 1).ToList();
        for (var i = 0; i + 2 < indices.Count; i += 3)
            faces.Add(new Integer3(indices[i], indices[i + 1], indices[i + 2]));
        return faces;
    }

    static IEnumerable<int> FlattenPositiveIndices(StepDocument doc, StepToken token)
    {
        if (token.IsNumber)
            yield return (int)token.AsNumber();
        if (token.IsList)
        {
            foreach (var child in token.AsList(doc))
            {
                foreach (var idx in FlattenPositiveIndices(doc, child))
                    yield return idx;
            }
        }
    }

    static List<int> ReadPositiveIndices(IfcEntity entity, int index)
    {
        var token = entity.GetValue(index);
        return FlattenPositiveIndices(entity.Document, token).Select(i => i - 1).ToList();
    }

    static List<List<int>> ReadNestedPositiveIndices(IfcEntity entity, int index)
    {
        var token = entity.GetValue(index);
        if (!token.IsList)
            return [];
        var holes = new List<List<int>>();
        foreach (var child in token.AsList(entity.Document))
        {
            var ring = FlattenPositiveIndices(entity.Document, child).Select(i => i - 1).ToList();
            if (ring.Count >= 3)
                holes.Add(ring);
        }
        return holes;
    }

    /// <summary>Triangulates one face of the set in 3D (<see cref="ShellMesher"/>), wound the way
    /// its outer loop is; a degenerate face adds nothing.</summary>
    static void TriangulatePolygonFace(IReadOnlyList<Point3D> coords, IReadOnlyList<int> indices, List<Integer3> faces)
        => ShellMesher.TriangulateFace(coords, indices, [], faces);

    static void TriangulatePolygonFaceWithVoids(IReadOnlyList<Point3D> coords, IfcEntity face, List<Integer3> faces)
    {
        var outer = ReadPositiveIndices(face, IfcIndexedPolygonalFaceWithVoids.Instance.CoordIndex.Index);
        var holes = ReadNestedPositiveIndices(face, IfcIndexedPolygonalFaceWithVoids.Instance.InnerCoordIndices.Index);
        ShellMesher.TriangulateFace(coords, outer, holes, faces);
    }
}
