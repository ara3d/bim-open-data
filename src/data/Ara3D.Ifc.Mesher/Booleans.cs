using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Boolean ops: DIFFERENCE via half-space plane clipping (dominant clipping case).
/// General CSG union/intersection is unsupported and recorded as diagnostics.
/// </summary>
public static class Booleans
{
    public static TriangleMesh3D? BuildBooleanResult(MeshingContext ctx, IfcEntity booleanResult)
    {
        var op = booleanResult.GetString(IfcBooleanResult.Instance.Operator.Index);
        var first = MeshHelpers.ResolveRequired(ctx, booleanResult, IfcBooleanResult.Instance.FirstOperand);
        var second = MeshHelpers.ResolveRequired(ctx, booleanResult, IfcBooleanResult.Instance.SecondOperand);

        if (op.Contains(".DIFFERENCE."))
            return BuildDifference(ctx, first, second);

        ctx.Diagnostics.RecordUnsupported(booleanResult.GetEntityName(), $"Boolean operator {op} not implemented");
        return GeometryDispatcher.TryBuild(ctx, first);
    }

    public static TriangleMesh3D? BuildBooleanClippingResult(MeshingContext ctx, IfcEntity clip)
    {
        ctx.Diagnostics.RecordSupported("IFCBOOLEANCLIPPINGRESULT");
        return BuildBooleanResult(ctx, clip);
    }

    static TriangleMesh3D? BuildDifference(MeshingContext ctx, IfcEntity first, IfcEntity second)
    {
        var mesh = GeometryDispatcher.TryBuild(ctx, first);
        if (mesh is null)
            return null;

        if (second.GetEntityName() == "IFCPOLYGONALBOUNDEDHALFSPACE")
        {
            ctx.Diagnostics.RecordApproximate("IFCBOOLEANRESULT", "Polygonal-bounded half-space difference clipping");
            return ClipByPolygonalBoundedHalfSpace(ctx, mesh.Value, second);
        }

        if (second.GetEntityName() == "IFCHALFSPACESOLID")
        {
            ctx.Diagnostics.RecordApproximate("IFCBOOLEANRESULT", "Planar half-space difference clipping");
            return ClipByHalfSpace(ctx, mesh.Value, second);
        }

        ctx.Diagnostics.RecordUnsupported("IFCBOOLEANRESULT", $"Difference with {second.GetEntityName()} not supported");
        return mesh;
    }

    /// <summary>
    /// Which side of the base plane the difference keeps. IFC4 IfcHalfSpaceSolid.AgreementFlag: "TRUE if the
    /// normal to the base surface points away from the material of the half space". The material is what a
    /// difference removes, so TRUE keeps the positive side (the side the normal points to) and FALSE keeps the
    /// negative side. One rule for every normal: web-ifc agrees on the FZK-Haus roof planes (oblique) and the
    /// Mini Project, dental_clinic and steelplates horizontal ones (TKT-188, CorpusClipTests).
    /// </summary>
    static bool KeepsPositiveSide(bool agreement) => agreement;

    public static TriangleMesh3D ClipByHalfSpace(MeshingContext ctx, TriangleMesh3D mesh, IfcEntity halfSpace)
    {
        ctx.Diagnostics.RecordSupported("IFCHALFSPACESOLID");
        var agreement = halfSpace.GetString(IfcHalfSpaceSolid.Instance.AgreementFlag.Index).Contains(".T.");
        var plane = ReadHalfSpacePlane(ctx, halfSpace);
        var keepPositiveSide = KeepsPositiveSide(agreement);

        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        foreach (var face in mesh.FaceIndices)
        {
            var a = mesh.Points[face.A].Vector3;
            var b = mesh.Points[face.B].Vector3;
            var c = mesh.Points[face.C].Vector3;
            var refNormal = Vector3.Cross(b - a, c - a);
            var clipped = plane.ClipTriangle(a, b, c, keepPositiveSide);
            EmitClippedPolygon(points, faces, clipped, refNormal);
        }
        return new TriangleMesh3D(points, faces);
    }

    /// <summary>
    /// Clips <paramref name="mesh"/> by an IFCPOLYGONALBOUNDEDHALFSPACE: geometry is removed only where it is BOTH
    /// on the cut side of the base plane AND inside the polygonal bounding prism. The boundary (a 2D closed curve in
    /// the Position frame's XY plane) is extruded along local Z to form the prism. A per-triangle gate keeps any
    /// triangle whose centroid projects OUTSIDE the polygon fully intact, so this can never over-clip beyond a plain
    /// half-space clip (guaranteeing >= unclipped-baseline parity).
    /// </summary>
    static TriangleMesh3D ClipByPolygonalBoundedHalfSpace(
        MeshingContext ctx,
        TriangleMesh3D mesh,
        IfcEntity halfSpace)
    {
        ctx.Diagnostics.RecordSupported("IFCPOLYGONALBOUNDEDHALFSPACE");

        var agreement = halfSpace.GetString(IfcHalfSpaceSolid.Instance.AgreementFlag.Index).Contains(".T.");
        var plane = ReadHalfSpacePlane(ctx, halfSpace);
        var keepPositiveSide = KeepsPositiveSide(agreement);

        // Position frame + 2D polygonal boundary (bounding-prism cross-section, in the frame's XY plane).
        var positionFrame = Placements.ReadAxis2Placement3D(ctx,
            MeshHelpers.ResolveRequired(ctx, halfSpace, IfcPolygonalBoundedHalfSpace.Instance.Position));
        var boundaryCurve = MeshHelpers.ResolveRequired(ctx, halfSpace, IfcPolygonalBoundedHalfSpace.Instance.PolygonalBoundary);
        var polygon = CurveEvaluator.Evaluate2D(ctx, boundaryCurve, dropClosure: true);

        // Without a usable boundary we cannot gate; keep the operand unclipped rather than risk over-clipping.
        if (polygon.Count < 3)
            return mesh;

        var points = new List<Point3D>();
        var faces = new List<Integer3>();
        foreach (var face in mesh.FaceIndices)
        {
            var a = mesh.Points[face.A].Vector3;
            var b = mesh.Points[face.B].Vector3;
            var c = mesh.Points[face.C].Vector3;

            var centroid = (a + b + c) / 3f;
            var local = positionFrame.ToLocal(centroid);
            if (!PointInPolygon(polygon, local.X, local.Y))
            {
                // Outside the bounding prism: this half-space does not remove it. Keep the triangle intact.
                var k0 = AddPoint(points, a);
                var k1 = AddPoint(points, b);
                var k2 = AddPoint(points, c);
                faces.Add(new Integer3(k0, k1, k2));
                continue;
            }

            // Inside the prism: apply the base-plane clip.
            var refNormal = Vector3.Cross(b - a, c - a);
            var clipped = plane.ClipTriangle(a, b, c, keepPositiveSide);
            EmitClippedPolygon(points, faces, clipped, refNormal);
        }
        return new TriangleMesh3D(points, faces);
    }

    /// <summary>
    /// Emits 1–2 triangles for a clipped polygon, choosing the quad diagonal that best preserves
    /// the input triangle's normal hemisphere.
    /// </summary>
    static void EmitClippedPolygon(
        List<Point3D> points,
        List<Integer3> faces,
        IReadOnlyList<Vector3> clipped,
        Vector3 referenceNormal)
    {
        if (clipped.Count == 3)
        {
            faces.Add(new Integer3(
                AddPoint(points, clipped[0]),
                AddPoint(points, clipped[1]),
                AddPoint(points, clipped[2])));
            return;
        }

        if (clipped.Count != 4)
            return;

        var i0 = AddPoint(points, clipped[0]);
        var i1 = AddPoint(points, clipped[1]);
        var i2 = AddPoint(points, clipped[2]);
        var i3 = AddPoint(points, clipped[3]);
        var scoreA = FaceNormalDot(points, i0, i1, i2, referenceNormal)
                   + FaceNormalDot(points, i0, i2, i3, referenceNormal);
        var scoreB = FaceNormalDot(points, i0, i1, i3, referenceNormal)
                   + FaceNormalDot(points, i1, i2, i3, referenceNormal);
        if (scoreA >= scoreB)
        {
            faces.Add(new Integer3(i0, i1, i2));
            faces.Add(new Integer3(i0, i2, i3));
        }
        else
        {
            faces.Add(new Integer3(i0, i1, i3));
            faces.Add(new Integer3(i1, i2, i3));
        }
    }

    static float FaceNormalDot(List<Point3D> points, int a, int b, int c, Vector3 referenceNormal)
    {
        var pa = points[a].Vector3;
        var pb = points[b].Vector3;
        var pc = points[c].Vector3;
        return Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), referenceNormal);
    }

    /// <summary>Even-odd ray-cast point-in-polygon test on the 2D boundary ring.</summary>
    static bool PointInPolygon(IReadOnlyList<Vector2> poly, float px, float py)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            float xi = poly[i].X, yi = poly[i].Y;
            float xj = poly[j].X, yj = poly[j].Y;
            if (((yi > py) != (yj > py)) &&
                (px < (xj - xi) * (py - yi) / (yj - yi) + xi))
                inside = !inside;
        }
        return inside;
    }

    static Plane ReadHalfSpacePlane(MeshingContext ctx, IfcEntity halfSpace)
    {
        var surface = MeshHelpers.ResolveRequired(ctx, halfSpace, IfcHalfSpaceSolid.Instance.BaseSurface);
        if (surface.GetEntityName() != "IFCPLANE")
            throw new NotSupportedException($"Half space base {surface.GetEntityName()} not supported");

        var placement = Placements.ReadAxis2Placement3D(ctx,
            MeshHelpers.ResolveRequired(ctx, surface, IfcElementarySurface.Instance.Position));
        return new Plane(placement.Origin.Vector3, placement.Z);
    }

    static int AddPoint(List<Point3D> points, Vector3 p)
    {
        const float eps = 1e-5f;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Vector3.DistanceSquared(p) < eps * eps)
                return i;
        }
        var idx = points.Count;
        points.Add(p);
        return idx;
    }

    public static void RecordOpeningDiagnostic(MeshingContext ctx)
        => ctx.Diagnostics.RecordUnsupported("IFCRELVOIDSELEMENT", "Openings not subtracted in first pass");
}
