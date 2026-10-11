using Ara3D.BimOpenSchema.IO.Fragments.Schema;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Fragments.Tests;

/// <summary>Reading hand-built Fragments buffers: value typing, absence, and the format checks.</summary>
public class ReaderTests
{
    private static TestModel Storey() => new()
    {
        Items =
        {
            new(10, "IFCBUILDINGSTOREY", "storey-guid", ["""["Name","Level 1","IFCLABEL"]""", """["Elevation",0,"IFCLENGTHMEASURE"]"""],
                ["""["ContainsElements",20]"""]),
            new(20, "IFCWALL", "wall-guid",
                [
                    """["Name","Wall A","IFCLABEL"]""",
                    """["IsExternal",true,"IFCBOOLEAN"]""",
                    """["NumberOfRiser",12,"IFCINTEGER"]""",
                    """["Width",3,"IFCPOSITIVELENGTHMEASURE"]""",
                    """["Tag","","IFCIDENTIFIER"]""",
                    """["LongName",null,"IFCLABEL"]""",
                    """["Layers",[1,2],"IFCREAL"]""",
                ],
                ["""["ContainedInStructure",10]""", """["Material",999]"""]),
            new(30, "IFCPROPERTYSINGLEVALUE", null, ["""["Name","Reference","IFCIDENTIFIER"]"""]),
        },
    };

    private static BimData Read(TestModel model) => FragmentsToBos.Read(model.ToBytes(), "test", "test.frag");

    private static List<ParameterValue> WallParameters(BimData d) => d.ParametersOf(d.EntityByLocalId(20)).ToList();

    [Test]
    public void AttributeValues_KeepTheTypeTheFileStates()
    {
        var d = Read(Storey());
        Assert.That(WallParameters(d), Is.EquivalentTo(new[]
        {
            new ParameterValue("IFCWALL", "Ifc:IsExternal", ParameterType.Int, 1),
            new ParameterValue("IFCWALL", "Ifc:NumberOfRiser", ParameterType.Int, 12),
            new ParameterValue("IFCWALL", "Ifc:Width", ParameterType.Number, 3f),
            new ParameterValue("IFCWALL", "Ifc:Tag", ParameterType.String, ""),
            new ParameterValue("IFCWALL", "Ifc:Layers", ParameterType.String, "[1,2]"),
        }));
    }

    [Test]
    public void NullAttribute_AddsNoParameter()
        => Assert.That(WallParameters(Read(Storey())).Select(p => p.Name), Has.None.EqualTo("Ifc:LongName"));

    [Test]
    public void ContainedInStructure_BecomesContainedIn_AndTheMirrorIsNotRepeated()
    {
        var d = Read(Storey());
        Assert.That(d.RelationPairs(RelationType.ContainedIn), Is.EquivalentTo(new[] { ("wall-guid", "storey-guid") }));
        Assert.That(d.Relations, Has.Length.EqualTo(1));
    }

    [Test]
    public void RelationToAnAbsentItem_IsDroppedAndReported()
    {
        var d = Read(Storey());
        Assert.Multiple(() =>
        {
            Assert.That(WallParameters(d).Select(p => p.Name), Has.None.EqualTo("Ifc:Material"));
            Assert.That(d.Diagnostics.Select(x => d.Strings[(int)x.Message]),
                Has.One.EqualTo("1 targets of relation 'Material' are not items in the file and were dropped"));
        });
    }

    [Test]
    public void Entities_CarryLocalIdGlobalIdNameAndCategory()
    {
        var d = Read(Storey());
        var wall = d.EntityByLocalId(20);
        var property = d.EntityByLocalId(30);
        Assert.Multiple(() =>
        {
            Assert.That((d.GlobalId(wall), d.Name(wall), d.Category(wall)), Is.EqualTo(("wall-guid", "Wall A", "IFCWALL")));
            Assert.That(d.GlobalId(property), Is.Empty, "BOS writes an entity without a GlobalId as the empty string");
            Assert.That(d.Documents, Has.Length.EqualTo(1));
            Assert.That(d.Entities.Count(e => e.Document != d.Entities[wall].Document), Is.Zero);
        });
    }

    [Test]
    public void RawBufferWithIdentifier0001_IsRead()
    {
        var model = Storey();
        model.Deflate = false;
        model.Identifier = FragmentsFile.FileIdentifier;
        Assert.That(Read(model).Entities, Has.Length.EqualTo(Read(Storey()).Entities.Length));
    }

    [Test]
    public void AnotherIdentifier_FailsNamingIt()
    {
        var model = Storey();
        model.Identifier = "0002";
        Assert.That(() => Read(model), Throws.TypeOf<FragmentsFormatException>().With.Message.Contains("'0002'"));
    }

    [Test]
    public void AnotherMajorVersion_FailsNamingIt()
    {
        var model = Storey();
        model.Metadata = """{"version":"2.4.0"}""";
        Assert.That(() => Read(model), Throws.TypeOf<FragmentsFormatException>().With.Message.Contains("'2.4.0'"));
    }

    [Test]
    public void NoVersionInMetadata_IsReadAsFragments2()
    {
        var model = Storey();
        model.Metadata = null;
        Assert.That(Read(model).Entities, Is.Not.Empty);
    }

    [TestCase(new byte[] { })]
    [TestCase(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })]
    public void BytesThatAreNotAModel_FailWithAFormatException(byte[] bytes)
        => Assert.That(() => FragmentsToBos.Read(bytes, "x", "x.frag"), Throws.TypeOf<FragmentsFormatException>());

    [Test]
    public void Convert_WritesABosArchiveThatReadsBack()
    {
        var dir = Directory.CreateTempSubdirectory("fragments-tests-");
        try
        {
            var input = Path.Combine(dir.FullName, "storey.frag");
            var output = Path.Combine(dir.FullName, "storey.bos");
            File.WriteAllBytes(input, Storey().ToBytes());
            FragmentsToBos.Convert(input, output);
            var read = ((FilePath)output).ReadBimDataFromParquetZip();
            Assert.That(read.Entities, Has.Length.EqualTo(Read(Storey()).Entities.Length));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Test]
    public void InstancesOfAHiddenClass_AreFlaggedHidden_OthersAreNot()
    {
        var model = new TestModel
        {
            Items = { new(1, "IFCSPACE", "space-guid", []), new(2, "IFCWALL", "wall-guid", []) },
            Shells = { new([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]) },
            Representations = { (RepresentationClass.SHELL, 0) },
            MeshesItems = { 0, 1 },
            GlobalTransforms = { TestTransform.Identity, TestTransform.Identity },
            Samples = { new TestSample(Item: 0, Representation: 0), new TestSample(Item: 1, Representation: 0) },
        };
        var d = Read(model);
        var flagOf = Enumerable.Range(0, d.Geometry.InstanceFlags.Length)
            .ToDictionary(i => d.Category(d.Geometry.InstanceEntityIndex[i]), i => d.Geometry.InstanceFlags[i]);
        Assert.That(flagOf, Is.EquivalentTo(new Dictionary<string, byte> { ["IFCSPACE"] = 1, ["IFCWALL"] = 0 }));
    }

    /// <summary>Both readers take the hidden classes from Ara3D.Ifc.Conventions, so on the Duplex
    /// they hide the instances of the same entities, its 21 spaces, one instance each.</summary>
    [Test]
    public void Duplex_HidesTheSameEntitiesAsIfcBos()
    {
        var frag = FragmentsToBos.Read(DuplexFixtureTests.FixturePath);
        var ifc = ((FilePath)RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip();
        var fragHidden = HiddenEntities(frag);
        var ifcHidden = HiddenEntities(ifc);
        Assert.Multiple(() =>
        {
            Assert.That(fragHidden, Has.Count.EqualTo(HiddenEntityCountOnDuplex));
            Assert.That(fragHidden, Is.EquivalentTo(ifcHidden));
            Assert.That(HiddenInstanceCount(frag), Is.EqualTo(HiddenInstanceCount(ifc)));
            Assert.That(VisibleEntities(frag).Intersect(fragHidden), Is.Empty, "an entity is hidden in every instance or in none");
        });
    }

    private const int HiddenEntityCountOnDuplex = 21;

    private static bool IsHidden(BimGeometry g, int instance)
        => (g.InstanceFlags[instance] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0;

    private static int HiddenInstanceCount(BimData d)
        => Enumerable.Range(0, d.Geometry.InstanceFlags.Length).Count(i => IsHidden(d.Geometry, i));

    /// <summary>(category, GlobalId) of each entity with a hidden instance.</summary>
    private static HashSet<(string, string)> HiddenEntities(BimData d) => InstanceEntities(d, hidden: true);

    private static HashSet<(string, string)> VisibleEntities(BimData d) => InstanceEntities(d, hidden: false);

    private static HashSet<(string, string)> InstanceEntities(BimData d, bool hidden)
        => Enumerable.Range(0, d.Geometry.InstanceEntityIndex.Length)
            .Where(i => IsHidden(d.Geometry, i) == hidden)
            .Select(i => d.Geometry.InstanceEntityIndex[i])
            .Where(e => e >= 0)
            .Select(e => (d.Category(e), d.GlobalId(e)))
            .ToHashSet();
}
