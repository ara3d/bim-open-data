using System.Diagnostics;
using Ara3D.BimOpenSchema;
using Ara3D.Geometry;
using Ara3D.Models;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace BimOpenMcp.Ifc.Tests;

/// <summary>Not part of the gate: compares, per element (STEP id), the two geometry sources
/// ifc_export_glb could write from. One is the Approach1 mesher the session holds for ifc_mesh
/// (<see cref="IfcSession.Meshing"/>); the other is the IFC-to-BOS conversion
/// (<see cref="IfcSession.Bos"/>, web-ifc geometry through Ara3D.Ifc.Bos), which ifc_export_glb
/// writes from. Prints how long each took, a summary, and every element whose instance count,
/// triangle count or bounds differ. The numbers that chose the BOS conversion are in the commit
/// that added this file.</summary>
[Explicit("meshes and converts each model, seconds to minutes per model; run to compare the two geometry sources")]
public sealed class GlbSourceComparisonTests
{
    /// <summary>Bounds corners further apart than this, in metres, count as disagreeing.</summary>
    private const double BoundsTolerance = 0.01;

    private static IEnumerable<TestCaseData> Models()
    {
        yield return new TestCaseData(TestModel.FzkHaus).SetArgDisplayNames(TestModel.FzkHaus);
        yield return new TestCaseData("AC20-Institute-Var-2.ifc").SetArgDisplayNames("AC20-Institute-Var-2.ifc");
        yield return new TestCaseData("schependomlaan.ifc").SetArgDisplayNames("schependomlaan.ifc");
        yield return new TestCaseData(RepoPaths.Samples("nrc", "duplex-base.ifc")).SetArgDisplayNames("duplex-base.ifc");
    }

    [TestCaseSource(nameof(Models))]
    public void Measure_geometry_sources(string model)
    {
        var path = Path.IsPathRooted(model) ? model : TestModel.RequirePath(model);
        using var bosSessions = new BosSessionCache();
        using var session = new IfcSession(new FilePath(path), bosSessions);

        var watch = Stopwatch.StartNew();
        var approach1 = Measure(session.Model(), entity => entity);
        var approach1Ms = watch.ElapsedMilliseconds;

        watch.Restart();
        var data = session.Bos.Data;
        var bosMs = watch.ElapsedMilliseconds;
        var fromBos = Measure(data.Geometry.ToModel3D(), row => StepId(data, row));

        var shared = approach1.Keys.Intersect(fromBos.Keys).ToList();
        var gaps = shared.ToDictionary(id => id, id => Gap(approach1[id].Bounds, fromBos[id].Bounds));
        TestContext.Progress.WriteLine(
            $"{Path.GetFileName(path)}: Approach1 {approach1Ms} ms, BOS conversion {bosMs} ms (converted and read back); " +
            $"elements Approach1 {approach1.Count}, BOS {fromBos.Count}, both {shared.Count}; " +
            $"same instance count {shared.Count(id => approach1[id].Instances == fromBos[id].Instances)}, " +
            $"same triangle count {shared.Count(id => approach1[id].Triangles == fromBos[id].Triangles)}, " +
            $"bounds within {BoundsTolerance} m {gaps.Values.Count(g => g <= BoundsTolerance)}; " +
            $"triangles Approach1 {approach1.Values.Sum(e => e.Triangles)}, BOS {fromBos.Values.Sum(e => e.Triangles)}");

        foreach (var id in approach1.Keys.Union(fromBos.Keys).Order())
        {
            var a = approach1.GetValueOrDefault(id);
            var b = fromBos.GetValueOrDefault(id);
            var gap = gaps.GetValueOrDefault(id, double.NaN);
            if (a?.Instances == b?.Instances && a?.Triangles == b?.Triangles && gap <= BoundsTolerance)
                continue;
            var type = id < 0 ? "no entity" : session.Resolver.GetEntity(id).GetEntityName();
            var boxes = gap > BoundsTolerance ? $" Approach1 {a!.Bounds.Min}..{a.Bounds.Max}, BOS {b!.Bounds.Min}..{b.Bounds.Max}" : "";
            TestContext.Progress.WriteLine(
                $"  #{id} {type}: instances {a?.Instances}/{b?.Instances}, hidden {a?.Hidden}/{b?.Hidden}, " +
                $"triangles {a?.Triangles}/{b?.Triangles}, bounds gap {gap:F3} m{boxes}");
        }

        Assert.That(shared, Is.Not.Empty, "the two sources mesh no element in common");
    }

    /// <summary>What one source drew for one element: every instance that carries its STEP id.</summary>
    private sealed class ElementGeometry
    {
        public int Instances;
        public int Hidden;
        public long Triangles;
        public readonly List<Bounds3D> Boxes = [];
        public Bounds3D Bounds => Boxes.Bounds();
    }

    /// <summary>Groups a model's drawable instances by STEP id, in world coordinates.</summary>
    private static Dictionary<int, ElementGeometry> Measure(Model3D model, Func<int, int> stepId)
    {
        var result = new Dictionary<int, ElementGeometry>();
        foreach (var instance in model.Instances)
        {
            if (instance.MeshIndex < 0 || instance.MeshIndex >= model.Meshes.Count)
                continue;
            var mesh = model.Meshes[instance.MeshIndex];
            if (mesh.FaceIndices.Count == 0)
                continue;
            var id = stepId(instance.EntityIndex);
            if (!result.TryGetValue(id, out var element))
                result[id] = element = new ElementGeometry();
            element.Instances++;
            if ((instance.Flags & InstanceStruct.HiddenFlag) != 0)
                element.Hidden++;
            element.Triangles += mesh.FaceIndices.Count;
            var matrix = instance.Matrix4x4;
            element.Boxes.Add(mesh.Points.Select(p => p.Vector3.Transform(matrix)).Select(v => new Point3D(v.X, v.Y, v.Z)).Bounds());
        }
        return result;
    }

    /// <summary>The STEP id the converter stored as the entity's LocalId, or -1 for no entity.</summary>
    private static int StepId(IBimData data, int row)
        => row >= 0 && row < data.Entities.Length ? (int)data.Entities[row].LocalId : -1;

    /// <summary>The largest distance, on any axis, between the two boxes' min corners or max corners.</summary>
    private static double Gap(Bounds3D a, Bounds3D b)
        => new double[]
        {
            a.Min.X - b.Min.X, a.Min.Y - b.Min.Y, a.Min.Z - b.Min.Z,
            a.Max.X - b.Max.X, a.Max.Y - b.Max.Y, a.Max.Z - b.Max.Z,
        }.Max(Math.Abs);
}
