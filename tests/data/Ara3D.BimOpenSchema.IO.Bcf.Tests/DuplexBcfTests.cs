using System.Numerics;
using System.Xml.Linq;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataTable;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>One topic per door type of the committed sample <c>samples/public/duplex.bos</c>
/// (14 doors of 4 types), built from a SQL result over the model, written with and without
/// geometry, and checked against the buildingSMART BCF 3.0 schemas.</summary>
[TestFixture]
public sealed class DuplexBcfTests
{
    /// <summary>A plain SQL result is the issues table: no code between the query and the file.</summary>
    public const string DoorsByType = """
        SELECT GlobalId, 'Doors of type ' || Type AS Title,
               'Check the clear width of each ' || Type || ' door.' AS Description
        FROM EntityText WHERE Category = 'IFCDOOR' ORDER BY Type, GlobalId
        """;

    private IDataTable _doors = null!;
    private IReadOnlyList<BcfIssue> _issues = null!;
    private ElementBounds _bounds = null!;

    [OneTimeSetUp]
    public void LoadDuplex()
    {
        var data = new FilePath(RepoPaths.Samples("public", "duplex.bos")).ReadBimDataFromParquetZip();
        using (var conn = data.ToDuckDb())
            _doors = conn.Query(DoorsByType, "Doors");
        _issues = _doors.ToBcfIssues();
        _bounds = ElementBounds.FromBimData(data);
    }

    private IEnumerable<string> DoorIds
        => _doors.Rows.Select(r => (string)r[0]);

    [Test]
    public void WithGeometry_EveryFileValidatesAgainstTheBcf3Schemas()
    {
        var (archive, summary) = BcfArchive.Write(_issues, bounds: _bounds);
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(summary, Is.EqualTo(new BcfSummary(Topics: 4, Viewpoints: 4, Elements: 14, ElementsWithoutGeometry: 0)));
            Assert.That(archive.Files(".bcfv").Count(), Is.EqualTo(4), "one viewpoint per topic");
        });
    }

    [Test]
    public void WithGeometry_EachDoorIsSelectedOnceUnderAPerspectiveCamera()
    {
        var (archive, _) = BcfArchive.Write(_issues, bounds: _bounds);
        var selected = archive.Viewpoints.SelectMany(v => v.Descendants("Component")).Select(c => (string?)c.Attribute("IfcGuid")).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(selected, Is.EquivalentTo(DoorIds));
            Assert.That(archive.Viewpoints.All(v => v.Root!.Element("PerspectiveCamera") is not null), "every viewpoint has a perspective camera");
        });
    }

    [Test]
    public void WithGeometry_EachCameraSeesEveryCornerOfItsDoors()
    {
        var (archive, _) = BcfArchive.Write(_issues, bounds: _bounds);
        foreach (var issue in _issues)
        {
            var camera = Camera(archive.Xml($"{BcfGuid.Topic("", issue.Title)}/viewpoint.bcfv"));
            var box = _bounds.Union(issue.GlobalIds)!.Value;
            var halfFov = BcfArchive.Fixed.FieldOfView / 2;
            foreach (var corner in Corners(box))
            {
                var toCorner = Vector3.Normalize(corner - camera.Position);
                var angle = Math.Acos(Math.Clamp(Vector3.Dot(toCorner, camera.Direction), -1, 1)) * 180 / Math.PI;
                Assert.That(angle, Is.LessThanOrEqualTo(halfFov + 1e-3), $"{issue.Title}: corner {corner} is outside the view");
            }
        }
    }

    /// <summary>The boxes come from instance transforms over 0.1 mm vertices; door-sized boxes
    /// show the scale, rotation, and translation were applied in metres with z up.</summary>
    [Test]
    public void DoorBoxesAreDoorSized()
    {
        foreach (var id in DoorIds)
        {
            Assert.That(_bounds.TryGet(id, out var box), $"door {id} has geometry");
            var size = box.Size;
            Assert.Multiple(() =>
            {
                Assert.That(size.Z, Is.InRange(1.9f, 2.7f), $"door {id} height");
                Assert.That(Math.Max(size.X, size.Y), Is.InRange(0.7f, 1.5f), $"door {id} width");
            });
        }
    }

    /// <summary>An id that is not an IFC GlobalId (here a Revit UniqueId) is selected by
    /// AuthoringToolId, which BCF allows, rather than as an IfcGuid, which BCF would reject;
    /// having no geometry, it is counted and left out of the camera's box.</summary>
    [Test]
    public void ANonIfcIdIsSelectedByAuthoringToolId()
    {
        const string revitId = "6f4d6b3e-1c2a-4f7e-9a51-0e2b7c1d8a90-0004b1f2";
        var door = DoorIds.First();
        var (archive, summary) = BcfArchive.Write([new BcfIssue("Mixed sources", [door, revitId])], bounds: _bounds);
        var components = archive.Viewpoints.Single().Descendants("Component").ToList();
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That((string?)components[0].Attribute("IfcGuid"), Is.EqualTo(door));
            Assert.That((string?)components[1].Element("AuthoringToolId"), Is.EqualTo(revitId));
            Assert.That(summary.ElementsWithoutGeometry, Is.EqualTo(1));
        });
    }

    /// <summary>Characters XML cannot hold (U+0002 comes from an IFC \X\02 escape in a name) become
    /// U+FFFD in every field, so the archive is still written whole and still validates. A
    /// surrogate pair (here U+1F6AA, door) is kept; a lone surrogate is replaced.</summary>
    [Test]
    public void CharactersXmlCannotHoldAreReplacedAndEveryFileValidates()
    {
        var door = DoorIds.First();
        var issue = new BcfIssue("Door\u0002A", [door, "rv\u0004id"],
            Description: "lone \uD800 pair \uD83D\uDEAA end\u0001", Status: "Open\u0003", Priority: "\uFFFE", Type: "Check\u001F");
        var (archive, _) = BcfArchive.Write([issue], BcfArchive.Fixed with { Author = "a\u0000b" }, _bounds);
        var topic = archive.Markups.Single().Root!.Element("Topic")!;
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That((string?)topic.Element("Title"), Is.EqualTo("Door\uFFFDA"));
            Assert.That((string?)topic.Element("Description"), Is.EqualTo("lone \uFFFD pair \uD83D\uDEAA end\uFFFD"));
            Assert.That((string?)topic.Attribute("TopicStatus"), Is.EqualTo("Open\uFFFD"));
            Assert.That((string?)topic.Element("CreationAuthor"), Is.EqualTo("a\uFFFDb"));
            Assert.That((string?)archive.Viewpoints.Single().Descendants("AuthoringToolId").Single(), Is.EqualTo("rv\uFFFDid"));
        });
    }

    [Test]
    public void WithoutGeometry_NoViewpointNoCameraAndTheIdsAreInTheDescription()
    {
        var (archive, summary) = BcfArchive.Write(_issues);
        var descriptions = string.Join("\n", archive.Markups.Select(m => (string?)m.Descendants("Description").SingleOrDefault()));
        Assert.Multiple(() =>
        {
            Assert.That(archive.SchemaErrors(), Is.Empty);
            Assert.That(archive.Files(".bcfv"), Is.Empty);
            Assert.That(archive.Markups.SelectMany(m => m.Descendants("Viewpoints")), Is.Empty);
            Assert.That(summary, Is.EqualTo(new BcfSummary(Topics: 4, Viewpoints: 0, Elements: 14, ElementsWithoutGeometry: 14)));
            foreach (var id in DoorIds)
                Assert.That(descriptions, Does.Contain(id));
        });
    }

    [Test]
    public void TheSameInputWritesTheSameBytes()
    {
        var first = BcfArchive.Write(_issues, bounds: _bounds).Archive.Bytes;
        var second = BcfArchive.Write(_doors.ToBcfIssues(), bounds: _bounds).Archive.Bytes;
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void TheGuidSeedSeparatesTopicsOfDifferentChecks()
    {
        var plain = BcfArchive.Write(_issues).Archive.TopicFolders;
        var seeded = BcfArchive.Write(_issues, BcfArchive.Fixed with { GuidSeed = "door-width-check" }).Archive.TopicFolders;
        Assert.That(seeded.Intersect(plain), Is.Empty);
    }

    [Test]
    public void TheZipHasTheRootFilesThenAFolderEntryBeforeEachTopicsFiles()
    {
        var names = BcfArchive.Write(_issues, bounds: _bounds).Archive.EntryNames;
        var expected = new List<string> { "bcf.version", "extensions.xml" };
        foreach (var issue in _issues)
        {
            var guid = BcfGuid.Topic("", issue.Title);
            expected.AddRange([$"{guid}/", $"{guid}/markup.bcf", $"{guid}/viewpoint.bcfv"]);
        }
        Assert.That(names, Is.EqualTo(expected));
    }

    private static (Vector3 Position, Vector3 Direction) Camera(XDocument viewpoint)
    {
        var camera = viewpoint.Root!.Element("PerspectiveCamera")!;
        return (Vector(camera.Element("CameraViewPoint")!), Vector3.Normalize(Vector(camera.Element("CameraDirection")!)));
    }

    private static Vector3 Vector(XElement e)
        => new((float)e.Element("X")!, (float)e.Element("Y")!, (float)e.Element("Z")!);

    internal static IEnumerable<Vector3> Corners(ElementBox b)
        => from x in new[] { b.Min.X, b.Max.X }
           from y in new[] { b.Min.Y, b.Max.Y }
           from z in new[] { b.Min.Z, b.Max.Z }
           select new Vector3(x, y, z);
}
