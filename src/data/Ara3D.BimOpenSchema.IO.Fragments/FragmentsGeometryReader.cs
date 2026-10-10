using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Color = Ara3D.Geometry.Color;
using TriangleMesh3D = Ara3D.Geometry.TriangleMesh3D;
using SNMatrix = System.Numerics.Matrix4x4;
using V3 = System.Numerics.Vector3;
using FragmentsTransform = Ara3D.BimOpenSchema.IO.Fragments.Schema.Transform;
using Material = Ara3D.Models.Material;

namespace Ara3D.BimOpenSchema.IO.Fragments;

/// <summary>Turns the samples of a Fragments model into BOS geometry instances. A sample places
/// one representation (a shell or a circle extrusion) for one item, with one material, at
/// <c>global_transforms[item] * local_transforms[local_transform]</c> in That Open's y-up world.
/// That world is the model moved near the origin (web-ifc's COORDINATE_TO_ORIGIN), and
/// <c>meshes.coordinates</c> is the move; the BOS transform undoes it, then turns y-up to z-up,
/// so the geometry lands where the IFC placed it. Each representation becomes one mesh in its
/// own coordinates, shared by every sample that uses it.</summary>
internal sealed class FragmentsGeometryReader
{
    /// <summary>Row-vector matrix taking That Open's y-up world (three.js, from web-ifc) to BOS's
    /// z-up: (x, y, z) becomes (x, -z, y).</summary>
    public static readonly SNMatrix YUpToZUp = new(
        1, 0, 0, 0,
        0, 0, 1, 0,
        0, -1, 0, 0,
        0, 0, 0, 1);

    private readonly Meshes _meshes;
    private readonly IReadOnlyList<EntityIndex> _entityOfItem;
    private readonly SNMatrix _toBosWorld;
    private readonly BimGeometryBuilder _builder = new();
    private readonly Dictionary<uint, int> _meshOfRepresentation = new();
    private readonly Dictionary<uint, int> _materialOf = new();
    private readonly Dictionary<string, int> _problems = new();

    public FragmentsGeometryReader(Meshes meshes, IReadOnlyList<EntityIndex> entityOfItem)
    {
        _meshes = meshes;
        _entityOfItem = entityOfItem;
        _toBosWorld = YUpToZUp;
        if (meshes.Coordinates is not { } coordinates)
            Count("models without a coordinates transform: geometry is left where the file has it");
        else if (SNMatrix.Invert(Matrix(coordinates), out var undoMove))
            _toBosWorld = undoMove * YUpToZUp;
        else
            Count("models whose coordinates transform cannot be inverted: geometry is left where the file has it");
    }

    /// <summary>Problems met, each with the number of times: samples or profiles left out.</summary>
    public IReadOnlyDictionary<string, int> Problems => _problems;

    public BimGeometry Read()
    {
        for (var s = 0; s < _meshes.SamplesLength; s++)
            AddSample(_meshes.Samples(s)!.Value);
        return _builder.BuildModel();
    }

    private void AddSample(Sample sample)
    {
        if (sample.Item >= _meshes.MeshesItemsLength || sample.Item >= _meshes.GlobalTransformsLength
            || sample.LocalTransform >= _meshes.LocalTransformsLength || sample.Material >= _meshes.MaterialsLength
            || sample.Representation >= _meshes.RepresentationsLength)
        {
            Count("samples left out: an index lies outside its table");
            return;
        }
        var item = _meshes.MeshesItems((int)sample.Item);
        if (item >= _entityOfItem.Count)
        {
            Count("samples left out: meshes_items names no item");
            return;
        }
        var mesh = MeshOf(sample.Representation);
        if (mesh < 0)
            return;

        var transform = Matrix(_meshes.LocalTransforms((int)sample.LocalTransform)!.Value)
            * Matrix(_meshes.GlobalTransforms((int)sample.Item)!.Value)
            * _toBosWorld;
        _builder.AddInstance((int)_entityOfItem[(int)item], MaterialOf(sample.Material), mesh, _builder.AddTransform(transform), 0);
    }

    private int MeshOf(uint representation)
    {
        if (_meshOfRepresentation.TryGetValue(representation, out var mesh))
            return mesh;
        var rep = _meshes.Representations((int)representation)!.Value;
        TriangleMesh3D? triangles = null;
        switch (rep.RepresentationClass)
        {
            case RepresentationClass.SHELL when rep.Id < _meshes.ShellsLength:
                var (shellMesh, skipped) = ShellMesher.Triangulate(ShellLoops.From(_meshes.Shells((int)rep.Id)!.Value));
                if (skipped > 0)
                    Count("shell profiles left out: degenerate, or an index outside the shell's points", skipped);
                triangles = shellMesh;
                break;
            case RepresentationClass.CIRCLE_EXTRUSION when rep.Id < _meshes.CircleExtrusionsLength:
                triangles = CircleExtrusionMesher.Tessellate(_meshes.CircleExtrusions((int)rep.Id)!.Value);
                break;
            default:
                Count($"representations left out: class {rep.RepresentationClass} with id {rep.Id} has no geometry to read");
                break;
        }
        mesh = triangles == null ? -1 : _builder.AddMesh(triangles.Value);
        _meshOfRepresentation.Add(representation, mesh);
        return mesh;
    }

    private int MaterialOf(uint index)
    {
        if (_materialOf.TryGetValue(index, out var material))
            return material;
        var m = _meshes.Materials((int)index)!.Value;
        var color = new Color(m.R / 255f, m.G / 255f, m.B / 255f, m.A / 255f);
        material = _builder.AddMaterial(Material.Default.WithColor(color));
        _materialOf.Add(index, material);
        return material;
    }

    /// <summary>A Fragments transform as a row-vector matrix: the rows are the x and y directions,
    /// their cross product (z), and the position.</summary>
    public static SNMatrix Matrix(FragmentsTransform t)
    {
        var x = new V3(t.XDirection.X, t.XDirection.Y, t.XDirection.Z);
        var y = new V3(t.YDirection.X, t.YDirection.Y, t.YDirection.Z);
        var z = V3.Cross(x, y);
        return new SNMatrix(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            (float)t.Position.X, (float)t.Position.Y, (float)t.Position.Z, 1);
    }

    private void Count(string problem, int n = 1)
        => _problems[problem] = _problems.GetValueOrDefault(problem) + n;
}
