using System.Numerics;

namespace Ara3D.BimOpenSchema.ObjectModel.Tests;

/// <summary>
/// A hand-built BOS model with every fault the view must find once: indices out of range,
/// short columns, a hidden instance, an empty mesh, identity strings stored empty or missing,
/// and parameter values that are missing or of another type.
/// </summary>
[TestFixture]
public sealed class BosSceneTests
{
    private const int Wall = 0, Door = 1, Nameless = 2, Storey = 3, HiddenOnly = 4, EmptyOnly = 5;
    private const int NumberD = 0, StringD = 1, IntD = 2, EntityD = 3, PointD = 4;

    // Strings: 0 is "" (how converters store an unset value today).
    private static readonly string[] Strings = ["", "gid0", "Wall", "Door"];

    /// <summary>
    /// Instances, by index:
    /// 0 wall, identity; 1 wall, moved (material 7 is no row); 2 door, mesh 9 (no row);
    /// 3 door, transform 5 (no row); 4 hidden-only entity; 5 entity 99 (no row);
    /// 6 empty mesh; 7 door, past the end of InstanceFlags and InstanceMaterialIndex;
    /// 8 past the end of InstanceEntityIndex too.
    /// </summary>
    private static BimData Model() => new()
    {
        Strings = Strings,
        Numbers = [2.5f],
        Points = [new Point(1, 2, 3)],
        Entities =
        [
            Entity(Wall, (StringIndex)1, (StringIndex)2),
            Entity(Door, (StringIndex)0, (StringIndex)3),
            Entity(Nameless, (StringIndex)(-1), (StringIndex)99),
            Entity(Storey, (StringIndex)0, (StringIndex)0),
            Entity(HiddenOnly, (StringIndex)0, (StringIndex)0),
            Entity(EmptyOnly, (StringIndex)0, (StringIndex)0),
        ],
        Descriptors =
        [
            new((StringIndex)0, (StringIndex)0, (StringIndex)0, ParameterType.Number),
            new((StringIndex)0, (StringIndex)0, (StringIndex)0, ParameterType.String),
            new((StringIndex)0, (StringIndex)0, (StringIndex)0, ParameterType.Int),
            new((StringIndex)0, (StringIndex)0, (StringIndex)0, ParameterType.Entity),
            new((StringIndex)0, (StringIndex)0, (StringIndex)0, ParameterType.Point),
        ],
        Parameters =
        [
            Param(Wall, NumberD, 0),       // 0: 2.5
            Param(Door, NumberD, -1),      // 1: missing number
            Param(Wall, StringD, 0),       // 2: stored empty: a value
            Param(Wall, StringD, 99),      // 3: string past the end
            Param(Wall, IntD, -5),         // 4: an int is always present
            Param(Wall, EntityD, Storey),  // 5
            Param(Wall, EntityD, 42),      // 6: entity past the end
            Param(Wall, PointD, 0),        // 7
            Param(Wall, PointD, -1),       // 8: missing point
            Param(Wall, 9, 0),             // 9: descriptor past the end
            Param(99, NumberD, 0),         // 10: entity past the end
        ],
        Relations =
        [
            new((EntityIndex)Wall, (EntityIndex)Storey, RelationType.ContainedIn),
            new((EntityIndex)Door, (EntityIndex)Storey, RelationType.ContainedIn),
            new((EntityIndex)Door, (EntityIndex)Wall, RelationType.HostedBy),
            new((EntityIndex)(-1), (EntityIndex)Wall, RelationType.PartOf),
        ],
        Geometry = new BimGeometry
        {
            // Mesh 0: a triangle spanning (0, 0, 0) to (1, 2, 3) metres. Mesh 1: empty.
            VertexX = [0, 10_000, 0],
            VertexY = [0, 0, 20_000],
            VertexZ = [0, 0, 30_000],
            IndexBuffer = [0, 1, 2],
            MeshVertexOffset = [0, 3],
            MeshIndexOffset = [0, 3],
            MaterialRed = [255], MaterialGreen = [0], MaterialBlue = [0], MaterialAlpha = [255],
            MaterialMetallic = [0], MaterialRoughness = [128],
            // Transform 1: scale 2, a quarter turn about z, then move by (10, 20, 30).
            TransformTX = [0, 10], TransformTY = [0, 20], TransformTZ = [0, 30],
            TransformQX = [0, 0], TransformQY = [0, 0], TransformQZ = [0, MathF.Sqrt(0.5f)], TransformQW = [1, MathF.Sqrt(0.5f)],
            TransformSX = [1, 2], TransformSY = [1, 2], TransformSZ = [1, 2],
            InstanceEntityIndex = [Wall, Wall, Door, Door, HiddenOnly, 99, EmptyOnly, Door],
            InstanceMeshIndex = [0, 0, 9, 0, 0, 0, 1, 0, 0],
            InstanceMaterialIndex = [0, 7, 0, -1, 0, 0, 0],
            InstanceTransformIndex = [0, 1, 0, 5, 0, 0, 0, 0, 0],
            InstanceFlags = [0, 0, 0, 0, 1],
        },
    };

    private static Entity Entity(int localId, StringIndex globalId, StringIndex name)
        => new(localId, globalId, (DocumentIndex)(-1), name, (EntityIndex)(-1), (EntityIndex)(-1));

    private static Parameter Param(int entity, int descriptor, int value)
        => new((EntityIndex)entity, (DescriptorIndex)descriptor, value);

    private static readonly BosScene Scene = new(Model());

    private static Parameter Row(int row) => Scene.Data.Parameters[row];

    private static int[] Indices(BosSelection s) => s.Instances.Select(i => i.Index).ToArray();

    [Test]
    public void Bad_indices_are_found_once_and_counted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.InstanceCount, Is.EqualTo(9));
            Assert.That(Scene.InstancesWithBadMesh, Is.EqualTo(1), "instance 2: mesh 9");
            Assert.That(Scene.InstancesWithBadTransform, Is.EqualTo(1), "instance 3: transform 5");
            Assert.That(Scene.InstancesWithBadMaterial, Is.EqualTo(1), "instance 1: material 7; -1 and a short column are not faults");
            Assert.That(Scene.InstancesWithoutEntity, Is.EqualTo(2), "instance 5: entity 99; instance 8: past the column's end");
            Assert.That(Enumerable.Range(0, 9).Where(Scene.IsDrawable), Is.EqualTo(new[] { 0, 1, 4, 5, 6, 7, 8 }));
        });
    }

    [Test]
    public void An_instance_reads_an_invalid_index_as_minus_one()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.Instance(0), Is.EqualTo(new BosInstance(0, Wall, 0, 0, 0, false)));
            Assert.That(Scene.Instance(1).Material, Is.EqualTo(-1), "material 7");
            Assert.That(Scene.Instance(2).Mesh, Is.EqualTo(-1), "mesh 9");
            Assert.That(Scene.Instance(3).Transform, Is.EqualTo(-1), "transform 5");
            Assert.That(Scene.Instance(3).IsDrawable, Is.False);
            Assert.That(Scene.Instance(5).Entity, Is.EqualTo(-1), "entity 99");
            Assert.That(Scene.Instance(8).Entity, Is.EqualTo(-1), "past the end of InstanceEntityIndex");
            Assert.That(Scene.Instance(4).IsHidden, Is.True);
            Assert.That(Scene.Instance(7).IsHidden, Is.False, "past the end of InstanceFlags: visible");
        });
    }

    [Test]
    public void Select_by_default_leaves_out_hidden_and_empty_and_counts_faults()
    {
        var s = Scene.Select();
        Assert.Multiple(() =>
        {
            Assert.That(Indices(s), Is.EqualTo(new[] { 0, 1, 5, 7, 8 }));
            Assert.That(s.SkippedBadMesh, Is.EqualTo(1));
            Assert.That(s.SkippedEmptyMesh, Is.EqualTo(1));
            Assert.That(s.SkippedBadTransform, Is.EqualTo(1));
            Assert.That(s.DefaultedMaterials, Is.EqualTo(1));
            Assert.That(s.UnmatchedEntities, Is.Zero, "no entities were asked for");
        });
    }

    [Test]
    public void Select_can_include_hidden_and_empty()
    {
        var s = Scene.Select(new BosSceneFilter { IncludeHidden = true, IncludeEmptyMeshes = true });
        Assert.Multiple(() =>
        {
            Assert.That(Indices(s), Is.EqualTo(new[] { 0, 1, 4, 5, 6, 7, 8 }));
            Assert.That(s.SkippedEmptyMesh, Is.Zero);
        });
    }

    [Test]
    public void Select_by_entity_counts_entities_with_nothing_selected()
    {
        var s = Scene.Select(new BosSceneFilter { Entities = [Door, HiddenOnly, Storey, 42, -1] });
        Assert.Multiple(() =>
        {
            Assert.That(Indices(s), Is.EqualTo(new[] { 7 }), "the door's one good instance");
            Assert.That(s.SkippedBadMesh, Is.EqualTo(1), "only the door's are counted");
            Assert.That(s.SkippedBadTransform, Is.EqualTo(1));
            Assert.That(s.UnmatchedEntities, Is.EqualTo(4), "hidden only, no geometry, out of range, and -1, which never matches an unassigned instance");
        });
    }

    [Test]
    public void Instances_are_grouped_by_entity_in_table_order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.InstancesOf(Wall).ToArray(), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(Scene.InstancesOf(Door).ToArray(), Is.EqualTo(new[] { 7 }), "undrawable instances are not grouped");
            Assert.That(Scene.InstancesOf(HiddenOnly).ToArray(), Is.EqualTo(new[] { 4 }), "hidden instances are");
            Assert.That(Scene.InstancesOf(Storey).ToArray(), Is.Empty);
            Assert.That(Scene.InstancesOf(99).ToArray(), Is.Empty);
            Assert.That(Scene.UnassignedInstances.ToArray(), Is.EqualTo(new[] { 5, 8 }));
            Assert.That(Scene.ElementsWithGeometry, Is.EqualTo(new[] { Wall, Door, HiddenOnly, EmptyOnly }));
        });
    }

    [Test]
    public void Bounds_unite_the_visible_instances_moved_into_place()
    {
        var box = Scene.Bounds(Wall);
        Assert.That(box, Is.Not.Null);
        // Instance 0: (0, 0, 0)-(1, 2, 3). Instance 1: scaled to (2, 4, 6), turned so x
        // spans [-4, 0] and y [0, 2], then moved to x [6, 10], y [20, 22], z [30, 36].
        AssertClose(box!.Value.Min, Vector3.Zero);
        AssertClose(box.Value.Max, new Vector3(10, 22, 36));
    }

    [Test]
    public void Bounds_are_null_without_a_visible_instance_with_vertices()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.Bounds(HiddenOnly), Is.Null, "hidden");
            Assert.That(Scene.Bounds(EmptyOnly), Is.Null, "empty mesh");
            Assert.That(Scene.Bounds(Storey), Is.Null, "no geometry");
            Assert.That(Scene.Bounds(-1), Is.Null, "no entity");
        });
    }

    [Test]
    public void Identity_strings_are_null_when_empty_or_missing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.EntityIds(Wall), Is.EqualTo(("gid0", "Wall")));
            Assert.That(Scene.EntityIds(Door), Is.EqualTo(((string?)null, "Door")), "GlobalId stored as \"\"");
            Assert.That(Scene.EntityIds(Nameless), Is.EqualTo(((string?)null, (string?)null)), "index -1 and an index past the end");
            Assert.That(Scene.EntityIds(-1), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That(Scene.EntityIds(99), Is.EqualTo(((string?)null, (string?)null)));
            Assert.That(Scene.Text((StringIndex)0), Is.EqualTo(""), "Text keeps an empty string");
            Assert.That(Scene.DocumentTitle((DocumentIndex)(-1)), Is.Null);
        });
    }

    [Test]
    public void Parameters_and_relations_are_grouped_by_entity_in_table_order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.ParametersOf(Wall).ToArray(), Is.EqualTo(new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.That(Scene.ParametersOf(Door).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(Scene.ParametersOf(99).ToArray(), Is.Empty, "row 10's entity is out of range");
            Assert.That(Scene.RelationsFrom(Door).ToArray(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(Scene.RelationsFrom(-1).ToArray(), Is.Empty);
        });
    }

    [Test]
    public void Typed_values_are_null_when_missing_never_zero()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.NumberValue(Row(0)), Is.EqualTo(2.5f));
            Assert.That(Scene.NumberValue(Row(1)), Is.Null, "number index -1");
            Assert.That(Scene.StringValue(Row(2)), Is.EqualTo(""), "an empty string is a value");
            Assert.That(Scene.StringValue(Row(3)), Is.Null, "string index past the end");
            Assert.That(Scene.IntValue(Row(4)), Is.EqualTo(-5));
            Assert.That(Scene.EntityValue(Row(5)), Is.EqualTo(Storey));
            Assert.That(Scene.EntityValue(Row(6)), Is.Null, "entity past the end");
            Assert.That(Scene.PointValue(Row(7)), Is.EqualTo(new Point(1, 2, 3)));
            Assert.That(Scene.PointValue(Row(8)), Is.Null, "point index -1");
            Assert.That(Scene.Descriptor(Row(9)), Is.Null, "descriptor past the end");
        });
    }

    [Test]
    public void A_value_read_as_another_type_is_null()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scene.NumberValue(Row(2)), Is.Null, "a string parameter");
            Assert.That(Scene.IntValue(Row(0)), Is.Null, "a number parameter");
            Assert.That(Scene.IntValue(Row(9)), Is.Null, "no descriptor");
        });
    }

    [Test]
    public void HasValue_matches_the_typed_values()
    {
        var expected = new[] { true, false, true, false, true, true, false, true, false, false };
        Assert.That(Enumerable.Range(0, expected.Length).Select(r => Scene.HasValue(Row(r))), Is.EqualTo(expected));
    }

    [Test]
    public void A_model_without_geometry_has_no_instances()
    {
        var scene = new BosScene(new BimData { Entities = [Entity(0, (StringIndex)(-1), (StringIndex)(-1))] });
        Assert.Multiple(() =>
        {
            Assert.That(scene.InstanceCount, Is.Zero);
            Assert.That(scene.Select().Instances, Is.Empty);
            Assert.That(scene.ElementsWithGeometry, Is.Empty);
            Assert.That(scene.Bounds(0), Is.Null);
        });
    }

    private static void AssertClose(Vector3 actual, Vector3 expected)
        => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{actual} is not {expected}");
}
