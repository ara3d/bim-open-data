using Ara3D.Utils;

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
}
