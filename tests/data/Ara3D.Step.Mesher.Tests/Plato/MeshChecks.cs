using Ara3D.Geometry;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>Closedness, orientation, Euler characteristic and volume of a contract mesh, computed in C# independently
/// of the Plato library, so the library's verdicts on its own mesh have a second witness.</summary>
public static class MeshChecks
{
    /// <summary>Directed edges that do not occur exactly once with their reverse occurring exactly once: empty for a
    /// closed, consistently oriented, edge-manifold mesh.</summary>
    public static IReadOnlyList<(int From, int To)> UnpairedEdges(TriangleMesh3D mesh)
    {
        var directed = DirectedEdges(mesh).GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
        return directed.Where(d => d.Value != 1 || directed.GetValueOrDefault((d.Key.To, d.Key.From)) != 1)
            .Select(d => d.Key).ToList();
    }

    /// <summary>V - E + F over the vertices the triangles use.</summary>
    public static int EulerCharacteristic(TriangleMesh3D mesh)
    {
        var edges = DirectedEdges(mesh).Select(e => (Math.Min(e.From, e.To), Math.Max(e.From, e.To))).Distinct().Count();
        var vertices = mesh.FaceIndices.SelectMany(f => new[] { f.A, f.B, f.C }).Distinct().Count();
        return vertices - edges + mesh.FaceIndices.Count;
    }

    /// <summary>The divergence-theorem volume in double precision: positive when the triangles face outward.</summary>
    public static double SignedVolume(TriangleMesh3D mesh)
    {
        var sum = 0.0;
        foreach (var f in mesh.FaceIndices)
        {
            var (a, b, c) = (mesh.Points[f.A], mesh.Points[f.B], mesh.Points[f.C]);
            sum += (double)a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y)
                - (double)a.Y * ((double)b.X * c.Z - (double)b.Z * c.X)
                + (double)a.Z * ((double)b.X * c.Y - (double)b.Y * c.X);
        }
        return sum / 6;
    }

    static IEnumerable<(int From, int To)> DirectedEdges(TriangleMesh3D mesh)
        => mesh.FaceIndices.SelectMany(f => new[] { ((int)f.A, (int)f.B), ((int)f.B, (int)f.C), ((int)f.C, (int)f.A) });
}
