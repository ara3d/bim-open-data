using System.IO.Compression;
using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Google.FlatBuffers;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>One item of a hand-built model; attributes and relations are the JSON strings the
/// importer writes, for example <c>["Name","Wall A","IFCLABEL"]</c> and <c>["ContainedInStructure",2]</c>.</summary>
public sealed record TestItem(uint LocalId, string Category, string? GlobalId, string[] Attributes, string[]? Relations = null);

/// <summary>Builds small Fragments 2 buffers with the generated FlatBuffers API, for cases the
/// Duplex fixture does not cover: missing values, a foreign identifier or version, raw buffers.</summary>
public sealed class TestModel
{
    public List<TestItem> Items { get; } = [];
    public string? Metadata { get; set; } = """{"schema":"IFC4","generator":"@thatopen/fragments","version":"3.4.8"}""";
    public string? Identifier { get; set; }
    public bool Deflate { get; set; } = true;

    public byte[] ToBytes()
    {
        var b = new FlatBufferBuilder(1024);
        var meshes = EmptyMeshes(b);

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

    private static Offset<Meshes> EmptyMeshes(FlatBufferBuilder b)
    {
        VectorOffset Empty() { b.StartVector(4, 0, 4); return b.EndVector(); }
        var items = Empty();
        var samples = Empty();
        var representations = Empty();
        var materials = Empty();
        var extrusions = Empty();
        var shells = Empty();
        var local = Empty();
        var global = Empty();
        Meshes.StartMeshes(b);
        Meshes.AddCoordinates(b, Transform.CreateTransform(b, 0, 0, 0, 1, 0, 0, 0, 1, 0));
        Meshes.AddMeshesItems(b, items);
        Meshes.AddSamples(b, samples);
        Meshes.AddRepresentations(b, representations);
        Meshes.AddMaterials(b, materials);
        Meshes.AddCircleExtrusions(b, extrusions);
        Meshes.AddShells(b, shells);
        Meshes.AddLocalTransforms(b, local);
        Meshes.AddGlobalTransforms(b, global);
        return Meshes.EndMeshes(b);
    }

    private static byte[] Zlib(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
            zlib.Write(raw);
        return output.ToArray();
    }
}
