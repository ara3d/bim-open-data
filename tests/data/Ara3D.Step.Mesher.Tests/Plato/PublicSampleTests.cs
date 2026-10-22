using System.Globalization;
using System.Text.RegularExpressions;

namespace Ara3D.Step.Mesher.Tests.Plato;

/// <summary>A public AP214 sample through StepMesher.Mesh (TKT-313 chunk E5): STEP Tools' io1-tc-214.stp, a part in
/// millimetres with surfaces of revolution and a 1e-13 mm uncertainty. The file is not committed; the toolkit's
/// scripts/fetch-step-samples.ps1 puts it in data/step-samples, and the test is skipped when it is not there.</summary>
public static class PublicSampleTests
{
    const string Sample = "steptools-ap214/io1-tc-214.stp";

    [Test]
    public static void Io1Tc214MeshesClosedWithBoundsInMetres()
    {
        var path = FindSample(Sample);
        if (path == null)
            Assert.Ignore($"data/step-samples/{Sample} not found; run scripts/fetch-step-samples.ps1 in bim-open-toolkit");
        var result = StepMesher.Mesh(path);
        Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
        Assert.That((result.GeometryPending, result.Diagnostics.DroppedFaces.Count), Is.EqualTo((0, 0)));
        Assert.That(result.MeshCount, Is.GreaterThan(0));
        foreach (var mesh in result.Model!.Meshes)
        {
            Assert.That(MeshChecks.UnpairedEdges(mesh), Is.Empty, "every solid closed and consistently oriented");
            Assert.That(MeshChecks.SignedVolume(mesh), Is.GreaterThan(0), "outward");
        }

        // The file's lengths are millimetres (SI_UNIT(.MILLI.,.METRE.)); the result is in metres. Its vertex points,
        // read as text, are a lower bound on the part's extent: the meshed bounds hold them scaled by 1/1000 and are
        // no more than twice as wide, which a missed or doubled unit factor (1000x) cannot satisfy.
        var vertices = VertexPointBounds(File.ReadAllText(path));
        var (min, max) = (result.Bounds.Min, result.Bounds.Max);
        var meshed = new[] { (double)min.X, min.Y, min.Z, max.X, max.Y, max.Z };
        for (var axis = 0; axis < 3; axis++)
        {
            var (lo, hi) = (vertices[axis] / 1000, vertices[axis + 3] / 1000);
            Assert.That(meshed[axis], Is.LessThanOrEqualTo(lo + 1e-6), $"axis {axis} min");
            Assert.That(meshed[axis + 3], Is.GreaterThanOrEqualTo(hi - 1e-6), $"axis {axis} max");
            Assert.That(meshed[axis + 3] - meshed[axis], Is.LessThanOrEqualTo(2 * (hi - lo) + 1e-6), $"axis {axis} extent");
        }
    }

    /// <summary>The file in data/step-samples of this repository or of a repository above it (bim-open-data sits under
    /// the toolkit's deps/), or null.</summary>
    static string? FindSample(string relative)
    {
        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data", "step-samples", relative);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>Min x, y, z then max x, y, z over the CARTESIAN_POINTs that VERTEX_POINTs cite, in the file's units.
    /// io1-tc-214 is one part with no placement between its vertices and the result's frame.</summary>
    static double[] VertexPointBounds(string text)
    {
        var points = Regex.Matches(text, @"#(\d+)\s*=\s*CARTESIAN_POINT\s*\(\s*'[^']*'\s*,\s*\(([^)]*)\)")
            .ToDictionary(m => m.Groups[1].Value,
                m => m.Groups[2].Value.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray());
        var cited = Regex.Matches(text, @"VERTEX_POINT\s*\(\s*'[^']*'\s*,\s*#(\d+)\s*\)").Select(m => points[m.Groups[1].Value]).ToList();
        Assert.That(cited, Is.Not.Empty, "vertex points");
        return Enumerable.Range(0, 3).Select(a => cited.Min(p => p[a]))
            .Concat(Enumerable.Range(0, 3).Select(a => cited.Max(p => p[a]))).ToArray();
    }
}
