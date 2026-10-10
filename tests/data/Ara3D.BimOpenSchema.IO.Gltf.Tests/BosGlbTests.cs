using System.Diagnostics;
using System.Numerics;
using Ara3D.BimOpenSchema.IO.Gltf;
using BimOpenData.TestSupport;
using SharpGLTF.Schema2;

namespace Ara3D.BimOpenSchema.IO.Gltf.Tests;

/// <summary>Writes samples/public/duplex.bos to .glb, reads the file back with SharpGLTF, and
/// checks it against the BOS tables: one node per visible instance, the same triangles, the
/// ids in each node's extras, and the model turned from z-up to y-up.</summary>
public class BosGlbTests
{
    private static readonly string DuplexBos = RepoPaths.Samples("public", "duplex.bos");

    private BimData _data = null!;
    private BimGeometry _geometry = null!;

    [OneTimeSetUp]
    public void LoadDuplex()
    {
        if (!File.Exists(DuplexBos))
            Assert.Ignore($"{DuplexBos} not found");
        _data = ParquetUtils.ReadBimDataFromParquetZip(DuplexBos);
        _geometry = _data.Geometry;
    }

    private static string OutputPath(string name)
        => Path.Combine(TestContext.CurrentContext.WorkDirectory, name);

    private bool IsHidden(int instance)
        => (_geometry.InstanceFlags[instance] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0;

    /// <summary>Triangles in a mesh, from the Meshes table's offsets alone.</summary>
    private long MeshTriangles(int mesh)
    {
        var end = mesh + 1 < _geometry.MeshIndexOffset.Length ? _geometry.MeshIndexOffset[mesh + 1] : _geometry.IndexBuffer.Length;
        return (end - _geometry.MeshIndexOffset[mesh]) / 3;
    }

    private List<int> VisibleInstances(Func<int, bool>? keep = null)
        => Enumerable.Range(0, _geometry.InstanceEntityIndex.Length)
            .Where(i => !IsHidden(i) && MeshTriangles(_geometry.InstanceMeshIndex[i]) > 0 && (keep?.Invoke(i) ?? true))
            .ToList();

    private string? GlobalId(int entity)
    {
        var s = (int)_data.Entities[entity].GlobalId;
        return s >= 0 && s < _data.Strings.Length ? _data.Strings[s] : null;
    }

    private static long NodeTriangles(Node node)
        => node.Mesh.Primitives.Sum(p => p.IndexAccessor.Count / 3);

    [Test]
    public void Duplex_writes_one_node_per_visible_instance_with_the_bos_triangle_count()
    {
        var path = OutputPath("duplex.glb");
        var watch = Stopwatch.StartNew();
        var summary = BosGlb.WriteGlb(DuplexBos, path);
        watch.Stop();

        var expected = VisibleInstances();
        var expectedTriangles = expected.Sum(i => MeshTriangles(_geometry.InstanceMeshIndex[i]));
        TestContext.Progress.WriteLine(
            $"duplex.glb: {summary.Nodes} nodes of {_geometry.InstanceEntityIndex.Length} instances, {summary.Meshes} meshes, " +
            $"{summary.Materials} materials, {summary.Triangles} triangles, {summary.Bytes} bytes, {watch.ElapsedMilliseconds} ms (read and write); {path}");

        Assert.That(summary.Nodes, Is.EqualTo(expected.Count));
        Assert.That(summary.Triangles, Is.EqualTo(expectedTriangles));
        Assert.That(summary.Bytes, Is.EqualTo(new FileInfo(path).Length));

        var glb = ModelRoot.Load(path);
        var nodes = glb.DefaultScene.VisualChildren.ToList();
        Assert.That(nodes, Has.Count.EqualTo(expected.Count));
        Assert.That(nodes.Sum(NodeTriangles), Is.EqualTo(expectedTriangles));
    }

    [Test]
    public void Duplex_nodes_carry_entity_index_and_global_id_in_extras()
    {
        var path = OutputPath("duplex-ids.glb");
        _data.WriteGlb(path);
        var nodes = ModelRoot.Load(path).DefaultScene.VisualChildren.ToList();
        var expected = VisibleInstances();

        Assert.That(nodes, Has.Count.EqualTo(expected.Count));
        for (var n = 0; n < nodes.Count; n++)
        {
            var entity = _geometry.InstanceEntityIndex[expected[n]];
            var extras = nodes[n].Extras;
            Assert.That(extras, Is.Not.Null, $"node {n}");
            Assert.That((int)extras![BosGlb.EntityIndexKey]!, Is.EqualTo(entity), $"node {n}");
            Assert.That((string?)extras[BosGlb.GlobalIdKey], Is.EqualTo(GlobalId(entity)), $"node {n}");
        }
        Assert.That(nodes.Select(n => (string?)n.Extras![BosGlb.GlobalIdKey]), Has.All.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Duplex_is_y_up_in_metres()
    {
        var path = OutputPath("duplex-axes.glb");
        _data.WriteGlb(path);
        var (glbMin, glbMax) = WorldBounds(ModelRoot.Load(path));

        // BOS world bounds, z-up, from the tables: vertex ints over VertexMultiplier, then each instance's transform.
        var bosMin = new Vector3(float.MaxValue);
        var bosMax = new Vector3(float.MinValue);
        foreach (var i in VisibleInstances())
        {
            Matrix4x4 m = _geometry.GetTransformMatrix(_geometry.InstanceTransformIndex[i]);
            var slice = _geometry.GetMeshSlice(_geometry.InstanceMeshIndex[i]);
            for (var v = slice.BaseVertex; v < slice.BaseVertex + slice.VertexCount; v++)
            {
                var p = Vector3.Transform(new Vector3(_geometry.VertexX[v], _geometry.VertexY[v], _geometry.VertexZ[v]) / BimGeometry.VertexMultiplier, m);
                bosMin = Vector3.Min(bosMin, p);
                bosMax = Vector3.Max(bosMax, p);
            }
        }
        TestContext.Progress.WriteLine($"BOS bounds {bosMin} to {bosMax}; glTF bounds {glbMin} to {glbMax}");

        // (x, y, z) in BOS is (x, z, -y) in glTF.
        const float tolerance = 1e-3f;
        Assert.That(glbMin.X, Is.EqualTo(bosMin.X).Within(tolerance));
        Assert.That(glbMax.X, Is.EqualTo(bosMax.X).Within(tolerance));
        Assert.That(glbMin.Y, Is.EqualTo(bosMin.Z).Within(tolerance));
        Assert.That(glbMax.Y, Is.EqualTo(bosMax.Z).Within(tolerance));
        Assert.That(glbMin.Z, Is.EqualTo(-bosMax.Y).Within(tolerance));
        Assert.That(glbMax.Z, Is.EqualTo(-bosMin.Y).Within(tolerance));
        // Metres, not millimetres: the duplex is about 8 m from its footings to its roof.
        Assert.That(glbMax.Y - glbMin.Y, Is.InRange(3f, 30f));
    }

    [Test]
    public void Entity_filter_writes_only_the_chosen_entities()
    {
        var visible = VisibleInstances();
        var chosen = visible.Select(i => _geometry.InstanceEntityIndex[i]).Distinct().Take(3).ToHashSet();
        var expected = VisibleInstances(i => chosen.Contains(_geometry.InstanceEntityIndex[i]));

        var path = OutputPath("duplex-filtered.glb");
        var summary = _data.WriteGlb(path, new GlbExportOptions { EntityIndices = chosen });
        var nodes = ModelRoot.Load(path).DefaultScene.VisualChildren.ToList();

        Assert.That(summary.Nodes, Is.EqualTo(expected.Count));
        Assert.That(nodes.Select(n => (int)n.Extras![BosGlb.EntityIndexKey]!).Distinct(), Is.EquivalentTo(chosen));
        Assert.That(summary.Triangles, Is.EqualTo(expected.Sum(i => MeshTriangles(_geometry.InstanceMeshIndex[i]))));
        Assert.That(summary.UnmatchedEntityIndices, Is.Zero);
    }

    [Test]
    public void Entity_filter_counts_indices_that_draw_nothing()
    {
        var drawn = VisibleInstances().Select(i => _geometry.InstanceEntityIndex[i]).ToHashSet();
        var withoutGeometry = Enumerable.Range(0, _data.Entities.Length).First(e => !drawn.Contains(e));
        var onlyHidden = Enumerable.Range(0, _geometry.InstanceEntityIndex.Length)
            .Where(IsHidden).Select(i => _geometry.InstanceEntityIndex[i]).First(e => !drawn.Contains(e));
        var outOfRange = new[] { -1, _data.Entities.Length, _data.Entities.Length + 157200 };
        var good = drawn.First();

        var summary = _data.ToGltf(new GlbExportOptions
            { EntityIndices = [good, good, withoutGeometry, onlyHidden, .. outOfRange] }).Summary;

        Assert.That(summary.Nodes, Is.GreaterThan(0));
        Assert.That(summary.UnmatchedEntityIndices, Is.EqualTo(2 + outOfRange.Length));
    }

    [Test]
    public void Entity_filter_that_matches_nothing_says_so()
    {
        // Revit element ids from duplex node names, shifted past the Entities table so none can land on a row by chance.
        var stepIds = new[] { 157200, 157607, 157950 }.Select(id => id + _data.Entities.Length).ToList();
        var summary = _data.ToGltf(new GlbExportOptions { EntityIndices = stepIds }).Summary;
        Assert.That(summary.Nodes, Is.Zero);
        Assert.That(summary.UnmatchedEntityIndices, Is.EqualTo(stepIds.Count));
    }

    [Test]
    public void Hidden_instances_are_written_only_when_asked()
    {
        var hidden = Enumerable.Range(0, _geometry.InstanceEntityIndex.Length)
            .Count(i => IsHidden(i) && MeshTriangles(_geometry.InstanceMeshIndex[i]) > 0);
        var (_, without) = _data.ToGltf();
        var (_, with) = _data.ToGltf(new GlbExportOptions { IncludeHidden = true });
        TestContext.Progress.WriteLine($"duplex: {hidden} hidden instances with triangles");
        Assert.That(with.Nodes - without.Nodes, Is.EqualTo(hidden));
    }

    /// <summary>Not part of the gate: prints the numbers the README quotes, for every public sample.</summary>
    [Test, Explicit("measurement for the README")]
    public void Measure_public_samples()
    {
        foreach (var bos in Directory.GetFiles(RepoPaths.Samples("public"), "*.bos").Order())
        {
            var path = OutputPath(Path.ChangeExtension(Path.GetFileName(bos), ".glb"));
            var watch = Stopwatch.StartNew();
            var data = ParquetUtils.ReadBimDataFromParquetZip(bos);
            var read = watch.ElapsedMilliseconds;
            var summary = data.WriteGlb(path);
            TestContext.Progress.WriteLine(
                $"{Path.GetFileName(bos)}: {new FileInfo(bos).Length} bytes in, {data.Geometry.InstanceEntityIndex.Length} instances, " +
                $"{summary.Nodes} nodes, {summary.Meshes} meshes, {summary.Triangles} triangles, {summary.Bytes} bytes out; " +
                $"read {read} ms, write {watch.ElapsedMilliseconds - read} ms");
        }
    }

    private static (Vector3 Min, Vector3 Max) WorldBounds(ModelRoot glb)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var node in glb.DefaultScene.VisualChildren)
        {
            var world = node.WorldMatrix;
            foreach (var p in node.Mesh.Primitives.SelectMany(prim => prim.GetVertexAccessor("POSITION").AsVector3Array()))
            {
                var w = Vector3.Transform(p, world);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
        }
        return (min, max);
    }
}
