using Ara3D.Step.Decode;
using Ara3D.StepBrep;
using G = Ara3D.Geometry;

namespace Ara3D.Step.Mesher.Plato;

/// <summary>One decoded body of a <see cref="StepPlatoMesh"/> as the contract's <see cref="StepShapeMesh"/>: the
/// assembled mesh restricted to the body's faces, its unused vertices dropped, and carried back from the model's
/// millimetres through the inverse of its occurrence's placement into the item's own coordinates, in metres.</summary>
public static class StepPlatoBodies
{
    public static StepShapeMesh ToShapeMesh(this StepPlatoMesh file, BodyIndex body, StepUnits units)
    {
        var faces = file.FacesOf(body);
        var restricted = file.Mesh.RestrictedToFaces(faces).ToTriangleMesh3D();
        var meshed = faces.Count(f => file.IsMeshed(f));
        var pending = faces.Count(f => file.IsPending(f));
        return new(restricted.ToItemMetres(file.OccurrenceOf(body).Placement, units.MetresPerUnit), meshed, faces.Count - meshed, pending);
    }

    /// <summary>The library mesh's used vertices, mapped by the inverse of <paramref name="placement"/> (the item's
    /// file units to the model's millimetres) and scaled to metres; triangles are reversed when the placement mirrors,
    /// so the item's mesh keeps its outward orientation.</summary>
    public static G.TriangleMesh3D ToItemMetres(this TriangleMesh3D mesh, AffineTransform3D placement, double metresPerUnit)
    {
        var placed = placement.ToAffine();
        var toItem = placed.Inverse() ?? throw new ArgumentException("The occurrence's placement is singular.", nameof(placement));
        var mirrored = placed.Determinant < 0;
        var newIndex = new int[mesh.Positions.Count];
        Array.Fill(newIndex, -1);
        var points = new List<G.Point3D>();
        var faces = new G.Integer3[mesh.Faces.Count];
        int Vertex(VertexIndex v)
        {
            if (newIndex[v.Value] < 0)
            {
                newIndex[v.Value] = points.Count;
                var p = mesh.Positions[v.Value];
                var local = toItem.Point(new Vec3(p.X, p.Y, p.Z)) * metresPerUnit;
                points.Add(new G.Point3D((float)local.X, (float)local.Y, (float)local.Z));
            }
            return newIndex[v.Value];
        }
        for (var i = 0; i < faces.Length; i++)
        {
            var f = mesh.Faces[i];
            var (a, b, c) = (Vertex(f.A), Vertex(f.B), Vertex(f.C));
            faces[i] = mirrored ? new G.Integer3(a, c, b) : new G.Integer3(a, b, c);
        }
        return new G.TriangleMesh3D(points, faces);
    }

    static Affine ToAffine(this AffineTransform3D t)
        => new(Row(t.Matrix.Row1), Row(t.Matrix.Row2), Row(t.Matrix.Row3), Row(t.Matrix.Row4));

    static Vec3 Row(Number3 r)
        => new(r.X, r.Y, r.Z);
}
