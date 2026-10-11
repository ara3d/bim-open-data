using System.Text.Json.Nodes;

namespace Ara3D.BimOpenSchema.ObjectModel.Tests;

/// <summary>
/// BimObjectModel reads absence the way BosScene does: a GlobalId, name, or document title
/// stored as "" (files written before -1 meant absent) is null, a number index of -1 is a
/// null value rather than an exception, a parameter whose entity or descriptor is not a row
/// is left out, and an InstanceFlags column shorter than the Instances table means visible.
/// </summary>
[TestFixture]
public sealed class BimObjectModelTests
{
    private const int Wall = 0, Legacy = 1, Level = 2;
    private const string Area = "Area";

    // Strings: 0 is "" (how files written before -1 meant absent store an unset value).
    private static readonly string[] Strings =
        ["", "gid-wall", "Wall", "Level 1", CommonRevitParameters.LevelElevation, CommonRevitParameters.ElementLevel, Area];

    private static BimData Data() => new()
    {
        Strings = Strings,
        Numbers = [3.5f],
        Points = [],
        Documents = [new(Title: (StringIndex)0, Path: (StringIndex)0)],
        Entities =
        [
            Entity(10, (StringIndex)1, (StringIndex)2),
            Entity(11, (StringIndex)0, (StringIndex)0),
            Entity(12, (StringIndex)(-1), (StringIndex)3),
        ],
        Descriptors =
        [
            new((StringIndex)4, (StringIndex)0, (StringIndex)0, ParameterType.Number),
            new((StringIndex)5, (StringIndex)0, (StringIndex)0, ParameterType.Entity),
            new((StringIndex)6, (StringIndex)0, (StringIndex)0, ParameterType.Number),
        ],
        Parameters =
        [
            Param(Wall, 1, Level),   // the wall is on Level 1, which has no elevation
            Param(Wall, 2, -1),      // the wall's area: number index -1, absent
            Param(Legacy, 1, Level),
            Param(Legacy, 9, 0),     // descriptor past the end
            Param(99, 2, 0),         // entity past the end
        ],
        Relations = [],
        Diagnostics = [],
        Geometry = new BimGeometry
        {
            VertexX = [0, 10_000, 0], VertexY = [0, 0, 10_000], VertexZ = [0, 0, 0],
            IndexBuffer = [0, 1, 2],
            MeshVertexOffset = [0], MeshIndexOffset = [0],
            MaterialRed = [255], MaterialGreen = [0], MaterialBlue = [0], MaterialAlpha = [255],
            MaterialMetallic = [0], MaterialRoughness = [128],
            TransformTX = [0], TransformTY = [0], TransformTZ = [0],
            TransformQX = [0], TransformQY = [0], TransformQZ = [0], TransformQW = [1],
            TransformSX = [1], TransformSY = [1], TransformSZ = [1],
            InstanceEntityIndex = [Wall, Wall],
            InstanceMeshIndex = [0, 0],
            InstanceMaterialIndex = [0, 0],
            InstanceTransformIndex = [0, 0],
            // Instance 1 is past the end of InstanceFlags.
            InstanceFlags = [(byte)BimGeometry.InstanceFlagEnum.IsHidden],
        },
    };

    private static Entity Entity(int localId, StringIndex globalId, StringIndex name)
        => new(localId, globalId, (DocumentIndex)0, name, (EntityIndex)(-1), (EntityIndex)(-1));

    private static Parameter Param(int entity, int descriptor, int value)
        => new((EntityIndex)entity, (DescriptorIndex)descriptor, value);

    private static readonly BimObjectModel Model = new(Data(), computeParametersAndRelations: true);

    [Test]
    public void Legacy_empty_identity_strings_read_as_null_like_BosScene()
    {
        var legacy = Model.Entities[Legacy];
        var scene = new BosScene(Data());
        Assert.Multiple(() =>
        {
            Assert.That(legacy.GlobalId, Is.Null);
            Assert.That(legacy.Name, Is.Null);
            Assert.That(legacy.DocumentTitle, Is.Null);
            Assert.That(legacy.GlobalId, Is.EqualTo(scene.GlobalId(Legacy)));
            Assert.That(legacy.Name, Is.EqualTo(scene.Name(Legacy)));
            Assert.That(Model.Entities[Wall].GlobalId, Is.EqualTo("gid-wall"));
            Assert.That(Model.Entities[Wall].Name, Is.EqualTo("Wall"));
        });
    }

    [Test]
    public void Entity_json_writes_an_absent_global_id_as_null_not_empty()
    {
        var json = Model.ToEntityJsonObjects(e => e.Index == (EntityIndex)Legacy).Single();
        var entity = json["entity"]!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(entity.ContainsKey("globalId"), Is.True);
            Assert.That(entity["globalId"], Is.Null);
            Assert.That(entity["name"], Is.Null);
        });
    }

    [Test]
    public void A_number_index_of_minus_one_is_a_null_value()
    {
        var wall = Model.Entities[Wall];
        Assert.Multiple(() =>
        {
            Assert.That(wall.ParameterValues.ContainsKey(Area), Is.True);
            Assert.That(wall.ParameterValues[Area], Is.Null);
            Assert.That(wall.GetParameterAsNumber(Area), Is.Null);
            Assert.That(wall.GetParameterAsInt(Area), Is.Null);
            Assert.That(BosEntityJson.ToEntityJsonObject(wall)["properties"]![Area], Is.Null);
        });
    }

    [Test]
    public void Absent_level_elevation_type_and_workset_are_null_not_zero_or_empty()
    {
        var wall = Model.Entities[Wall];
        Assert.Multiple(() =>
        {
            Assert.That(wall.LevelName, Is.EqualTo("Level 1"));
            Assert.That(wall.Elevation, Is.Null);
            Assert.That(wall.TypeName, Is.Null);
            Assert.That(wall.WorksetId, Is.Null);
        });
    }

    [Test]
    public void Parameters_whose_entity_or_descriptor_is_not_a_row_are_left_out()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Model.Entities[Legacy].Parameters, Has.Count.EqualTo(1));
            Assert.That(Model.Entities.Sum(e => e.Parameters.Count), Is.EqualTo(3));
            Assert.That(Model.Create(Data().Parameters[3]), Is.Null);
            Assert.That(Model.GetParameterValue(Data().Parameters[3]), Is.Null);
        });
    }

    [Test]
    public void A_short_InstanceFlags_column_means_visible()
    {
        var g = Data().Geometry;
        Assert.Multiple(() =>
        {
            Assert.That(g.GetInstanceFlags(0), Is.EqualTo((byte)BimGeometry.InstanceFlagEnum.IsHidden));
            Assert.That(g.GetInstanceFlags(1), Is.Zero);
            Assert.That(g.GetInstanceStruct(1).Flags, Is.Zero);
            Assert.That(Model.Entities[Wall].Instances, Has.Count.EqualTo(2));
        });
    }
}
