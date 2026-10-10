using System.IO.Compression;
using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Google.FlatBuffers;
using V3 = System.Numerics.Vector3;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>One item of a hand-built model; attributes and relations are the JSON strings the
/// importer writes, for example <c>["Name","Wall A","IFCLABEL"]</c> and <c>["ContainedInStructure",2]</c>.</summary>
public sealed record TestItem(uint LocalId, string Category, string? GlobalId, string[] Attributes, string[]? Relations = null);

/// <summary>A shell: points, profiles as point indices, and holes as (profile, loop).</summary>
public sealed record TestShell(V3[] Points, uint[][] Profiles, (ushort Profile, uint[] Loop)[]? Holes = null, bool Big = false);

/// <summary>A circle extrusion with one axis, whose parts are straight wires and arcs, in that order.</summary>
public sealed record TestTube(double Radius, (V3 P1, V3 P2)[] Wires, TestArc[]? Arcs = null);

/// <summary>An arc as Fragments stores it: x_direction is the axis it turns about, y_direction
/// points from the centre to its first point.</summary>
public sealed record TestArc(float Aperture, V3 Center, float Radius, V3 XDirection, V3 YDirection);

/// <summary>A Fragments transform: position and the x and y directions.</summary>
public sealed record TestTransform(V3 Position, V3 XDirection, V3 YDirection)
{
    public static readonly TestTransform Identity = new(V3.Zero, V3.UnitX, V3.UnitY);
    public static TestTransform At(float x, float y, float z) => Identity with { Position = new V3(x, y, z) };
}

/// <summary>A sample: the item position (into meshes_items and global_transforms), the
/// representation, the local transform, and the material.</summary>
public sealed record TestSample(int Item, int Representation, int LocalTransform = 0, int Material = 0);

/// <summary>Builds small Fragments 2 buffers with the generated FlatBuffers API, for cases the
/// Duplex fixture does not cover: missing values, a foreign identifier or version, raw buffers,
/// holes, big shells, and circle extrusions.</summary>
public sealed class TestModel
{
    public List<TestItem> Items { get; } = [];
    public string? Metadata { get; set; } = """{"schema":"IFC4","generator":"@thatopen/fragments","version":"3.4.8"}""";
    public string? Identifier { get; set; }
    public bool Deflate { get; set; } = true;

    public List<TestShell> Shells { get; } = [];
    public List<TestTube> Tubes { get; } = [];
    /// <summary>Representations as (class, index into Shells or Tubes).</summary>
    public List<(RepresentationClass Class, uint Id)> Representations { get; } = [];
    /// <summary>For each geometry item: its position in Items.</summary>
    public List<uint> MeshesItems { get; } = [];
    public List<TestTransform> GlobalTransforms { get; } = [];
    public List<TestTransform> LocalTransforms { get; } = [TestTransform.Identity];
    public List<(byte R, byte G, byte B, byte A)> Materials { get; } = [(200, 100, 50, 255)];
    public List<TestSample> Samples { get; } = [];
    public TestTransform Coordinates { get; set; } = TestTransform.Identity;

    public byte[] ToBytes()
    {
        var b = new FlatBufferBuilder(1024);
        var meshes = BuildMeshes(b);

        var guids = Items.Where(i => i.GlobalId != null).ToList();
        var guidsVector = Model.CreateGuidsVector(b, guids.Select(i => b.CreateString(i.GlobalId)).ToArray());
        var guidsItems = Model.CreateGuidsItemsVector(b, guids.Select(i => i.LocalId).ToArray());
        var localIds = Model.CreateLocalIdsVector(b, Items.Select(i => i.LocalId).ToArray());
        var categories = Model.CreateCategoriesVector(b, Items.Select(i => b.CreateString(i.Category)).ToArray());
        var attributes = Model.CreateAttributesVector(b, Items
            .Select(i => Schema.Attribute.CreateAttribute(b, Schema.Attribute.CreateDataVector(b, i.Attributes.Select(b.CreateString).ToArray())))
            .ToArray());
        var related = Items.Where(i => i.Relations is { Length: > 0 }).ToList();
        var relations = Model.CreateRelationsVector(b, related
            .Select(i => Relation.CreateRelation(b, Relation.CreateDataVector(b, i.Relations!.Select(b.CreateString).ToArray())))
            .ToArray());
        var relationsItems = Model.CreateRelationsItemsVector(b, related.Select(i => (int)i.LocalId).ToArray());
        var metadata = Metadata == null ? default : b.CreateString(Metadata);
        var guid = b.CreateString("test-model");

        Model.StartModel(b);
        if (Metadata != null)
            Model.AddMetadata(b, metadata);
        Model.AddGuids(b, guidsVector);
        Model.AddGuidsItems(b, guidsItems);
        Model.AddLocalIds(b, localIds);
        Model.AddCategories(b, categories);
        Model.AddMeshes(b, meshes);
        Model.AddAttributes(b, attributes);
        Model.AddRelations(b, relations);
        Model.AddRelationsItems(b, relationsItems);
        Model.AddGuid(b, guid);
        var root = Model.EndModel(b);
        if (Identifier == null)
            b.Finish(root.Value);
        else
            b.Finish(root.Value, Identifier);

        var raw = b.SizedByteArray();
        return Deflate ? Zlib(raw) : raw;
    }

    private Offset<Meshes> BuildMeshes(FlatBufferBuilder b)
    {
        var items = Meshes.CreateMeshesItemsVector(b, MeshesItems.ToArray());
        var samples = StructVector(b, 16, Samples, s => Sample.CreateSample(b, (uint)s.Item, (uint)s.Material, (uint)s.Representation, (uint)s.LocalTransform));
        var representations = StructVector(b, 32, Representations, r => Representation.CreateRepresentation(b, r.Id, 0, 0, 0, 0, 0, 0, r.Class));
        var materials = StructVector(b, 6, Materials, m => Schema.Material.CreateMaterial(b, m.R, m.G, m.B, m.A, RenderedFaces.ONE, Stroke.DEFAULT), alignment: 1);
        var tubes = Meshes.CreateCircleExtrusionsVector(b, Tubes.Select(t => BuildTube(b, t)).ToArray());
        var shells = Meshes.CreateShellsVector(b, Shells.Select(s => BuildShell(b, s)).ToArray());
        var local = StructVector(b, 48, LocalTransforms, t => Transform(b, t), alignment: 8);
        var global = StructVector(b, 48, GlobalTransforms, t => Transform(b, t), alignment: 8);
        Meshes.StartMeshes(b);
        Meshes.AddCoordinates(b, Transform(b, Coordinates));
        Meshes.AddMeshesItems(b, items);
        Meshes.AddSamples(b, samples);
        Meshes.AddRepresentations(b, representations);
        Meshes.AddMaterials(b, materials);
        Meshes.AddCircleExtrusions(b, tubes);
        Meshes.AddShells(b, shells);
        Meshes.AddLocalTransforms(b, local);
        Meshes.AddGlobalTransforms(b, global);
        return Meshes.EndMeshes(b);
    }

    private static Offset<Schema.Transform> Transform(FlatBufferBuilder b, TestTransform t)
        => Schema.Transform.CreateTransform(b, t.Position.X, t.Position.Y, t.Position.Z,
            t.XDirection.X, t.XDirection.Y, t.XDirection.Z, t.YDirection.X, t.YDirection.Y, t.YDirection.Z);

    private static Offset<Shell> BuildShell(FlatBufferBuilder b, TestShell s)
    {
        var holes = s.Holes ?? [];
        VectorOffset Empty() => Shell.CreateProfilesVector(b, []);
        var small = !s.Big;
        var profiles = small
            ? Shell.CreateProfilesVector(b, s.Profiles.Select(p => ShellProfile.CreateShellProfile(b, ShellProfile.CreateIndicesVector(b, p.Select(i => (ushort)i).ToArray()))).ToArray())
            : Empty();
        var smallHoles = small
            ? Shell.CreateHolesVector(b, holes.Select(h => ShellHole.CreateShellHole(b, ShellHole.CreateIndicesVector(b, h.Loop.Select(i => (ushort)i).ToArray()), h.Profile)).ToArray())
            : Empty();
        var bigProfiles = small
            ? Empty()
            : Shell.CreateBigProfilesVector(b, s.Profiles.Select(p => BigShellProfile.CreateBigShellProfile(b, BigShellProfile.CreateIndicesVector(b, p))).ToArray());
        var bigHoles = small
            ? Empty()
            : Shell.CreateBigHolesVector(b, holes.Select(h => BigShellHole.CreateBigShellHole(b, BigShellHole.CreateIndicesVector(b, h.Loop), h.Profile)).ToArray());
        var points = StructVector(b, 12, s.Points, p => FloatVector.CreateFloatVector(b, p.X, p.Y, p.Z));
        var faceIds = Shell.CreateProfilesFaceIdsVector(b, s.Profiles.Select((_, i) => (ushort)i).ToArray());
        Shell.StartShell(b);
        Shell.AddProfiles(b, profiles);
        Shell.AddHoles(b, smallHoles);
        Shell.AddPoints(b, points);
        Shell.AddBigProfiles(b, bigProfiles);
        Shell.AddBigHoles(b, bigHoles);
        Shell.AddType(b, s.Big ? ShellType.BIG : ShellType.NONE);
        Shell.AddProfilesFaceIds(b, faceIds);
        return Shell.EndShell(b);
    }

    private static Offset<CircleExtrusion> BuildTube(FlatBufferBuilder b, TestTube t)
    {
        var arcs = t.Arcs ?? [];
        var wires = StructVector(b, 24, t.Wires, w => Wire.CreateWire(b, w.P1.X, w.P1.Y, w.P1.Z, w.P2.X, w.P2.Y, w.P2.Z));
        var curves = StructVector(b, 44, arcs, a => CircleCurve.CreateCircleCurve(b, a.Aperture, a.Center.X, a.Center.Y, a.Center.Z, a.Radius,
            a.XDirection.X, a.XDirection.Y, a.XDirection.Z, a.YDirection.X, a.YDirection.Y, a.YDirection.Z));
        var order = Axis.CreateOrderVector(b, Enumerable.Range(0, t.Wires.Length).Concat(Enumerable.Range(0, arcs.Length)).Select(i => (uint)i).ToArray());
        var parts = Axis.CreatePartsVector(b, Enumerable.Repeat(AxisPartClass.WIRE, t.Wires.Length).Concat(Enumerable.Repeat(AxisPartClass.CIRCLE_CURVE, arcs.Length)).ToArray());
        var wireSets = Axis.CreateWireSetsVector(b, []);
        var axis = Axis.CreateAxis(b, wires, order, parts, wireSets, curves);
        var radius = CircleExtrusion.CreateRadiusVector(b, [t.Radius]);
        var axes = CircleExtrusion.CreateAxesVector(b, [axis]);
        return CircleExtrusion.CreateCircleExtrusion(b, radius, axes);
    }

    /// <summary>A vector of structs: FlatBuffers builds back to front, so items go in reverse.</summary>
    private static VectorOffset StructVector<T, TStruct>(FlatBufferBuilder b, int size, IReadOnlyList<T> items, Func<T, Offset<TStruct>> create, int alignment = 4)
        where TStruct : struct
    {
        b.StartVector(size, items.Count, alignment);
        for (var i = items.Count - 1; i >= 0; i--)
            create(items[i]);
        return b.EndVector();
    }

    private static byte[] Zlib(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(raw);
        return output.ToArray();
    }
}
