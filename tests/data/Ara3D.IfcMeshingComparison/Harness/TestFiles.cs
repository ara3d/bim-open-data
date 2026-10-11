using Ara3D.IfcLoader;
using Ara3D.Utils;
using BimOpenData.TestSupport;

namespace Ara3D.IfcMeshingComparison.Harness;

/// <summary>
/// Where the comparison finds its model files and puts what it writes. Models come from the
/// repository's <c>data/</c> folder (filled by <c>data/get-test-data.ps1</c>) and from the folders
/// named in <c>ARA3D_IFC_TEST_DIRS</c> (separated by <c>;</c>); a test whose file is absent is
/// ignored, not failed. Everything the harness writes goes under <c>artifacts/ifc-mesher/</c>,
/// which git ignores, never beside an input file.
/// </summary>
public static class TestFiles
{
    public const string ExtraFoldersVariable = "ARA3D_IFC_TEST_DIRS";

    public static readonly DirectoryPath RepositoryDataDir = new(RepoPaths.Data());

    public static readonly DirectoryPath ArtifactsDir = new(RepoPaths.Artifacts("ifc-mesher"));

    /// <summary>Web-ifc tessellations of the corpus, generated once per file and kept as the oracle.</summary>
    public static readonly DirectoryPath WebIfcBfastDir = ArtifactsDir.RelativeFolder("oracles");

    public static readonly DirectoryPath ReportsDir = ArtifactsDir.RelativeFolder("reports");

    /// <summary>Meshes and probes a test writes, named after the model file they came from.</summary>
    public static readonly DirectoryPath ProbesDir = ArtifactsDir.RelativeFolder("probes");

    /// <summary>The committed benchmark folder: the parity scorecard over the quick files is written
    /// here so a run can be compared with the last one in git.</summary>
    public static readonly DirectoryPath BenchDir = new(Path.Combine(RepoPaths.Root, "bench", "ifc-mesher"));

    /// <summary>Mesh dumps of the external meshers over the quick files, committed so the comparison
    /// runs without those tools installed; one folder per mesher name.</summary>
    public static readonly DirectoryPath CommittedDumpsDir = BenchDir.RelativeFolder("dumps");

    /// <summary>Mesh dumps of the external meshers over any other model; git-ignored.</summary>
    public static readonly DirectoryPath DumpsDir = ArtifactsDir.RelativeFolder("dumps");

    public static FilePath Example => ResolveIfc("example.ifc");
    public static FilePath IfcOpenHouse => ResolveIfc("IfcOpenHouse_IFC4.ifc");
    public static FilePath SampleEntities => ResolveIfc("Sample_entities.ifc");
    public static FilePath Issue044CompositeProfile => ResolveIfc("ISSUE_044_test_IFCCOMPOSITEPROFILEDEF.ifc");
    public static FilePath Issue171SurfaceCurveSwept => ResolveIfc("ISSUE_171_IfcSurfaceCurveSweptAreaSolid.ifc");
    public static FilePath Ac20FzkHaus => ResolveIfc("AC20-FZK-Haus.ifc");
    public static FilePath SteelPlates => ResolveIfc("steelplates.ifc");
    public static FilePath Railing => ResolveIfc("railing.ifc");
    public static FilePath AiscSculptureBrep => ResolveIfc("171210AISC_Sculpture_brep.ifc");
    public static FilePath Small => ResolveIfc("small.ifc");
    public static FilePath DentalClinic => ResolveIfc("dental_clinic.ifc");
    public static FilePath Duplex => ResolveIfc("duplex.ifc");
    public static FilePath OfficeA => ResolveIfc("Office_A_20110811.ifc");
    public static FilePath Schependomlaan => ResolveIfc("schependomlaan.ifc");
    public static FilePath DigitalHub => ResolveIfc("FM_ARC_DigitalHub.ifc");
    public static FilePath Primark => ResolveIfc("20210221PRIMARK.ifc");
    public static FilePath C20Institute => ResolveIfc("C20-Institute-Var-2.ifc");
    public static FilePath Ac11Institute => ResolveIfc("231110AC11-Institute-Var-2-IFC.ifc");

    public static IEnumerable<FilePath> QuickComparisonFiles()
        => new[] { IfcOpenHouse, Example, SteelPlates };

    /// <summary>Resolves an IFC by file name, or Ignores the test if missing.</summary>
    public static FilePath ResolveOrIgnore(string fileName)
    {
        var path = ResolveIfc(fileName);
        if (!path.Exists())
            Assert.Ignore($"Missing IFC test file: {fileName}");
        return path;
    }

    /// <summary>The first search folder holding <paramref name="fileName"/>, else its path under
    /// the repository's <c>data/</c>, which then does not exist.</summary>
    public static FilePath ResolveIfc(string fileName)
    {
        foreach (var folder in SearchFolders())
        {
            var candidate = folder.RelativeFile(fileName);
            if (candidate.Exists())
                return candidate;
        }
        return RepositoryDataDir.RelativeFile(fileName);
    }

    public static IEnumerable<DirectoryPath> SearchFolders()
    {
        yield return RepositoryDataDir;
        var extra = Environment.GetEnvironmentVariable(ExtraFoldersVariable);
        if (string.IsNullOrWhiteSpace(extra))
            yield break;
        foreach (var part in extra.Split([';', Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Directory.Exists(part))
                yield return new DirectoryPath(part);
        }
    }

    /// <summary>Every <c>.ifc</c> in the search folders, one per file name, the first folder winning.</summary>
    public static IEnumerable<FilePath> AllKnownFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in SearchFolders())
        {
            if (!folder.Exists())
                continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.ifc", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (seen.Add(name))
                    yield return new FilePath(file);
            }
        }
    }

    public static string DescribeSearchFolders()
        => string.Join("; ", SearchFolders().Select(f => f.ToString())) + $" (data/ and {ExtraFoldersVariable})";

    public static void RequireExists(FilePath path)
    {
        if (!path.Exists())
            Assert.Ignore($"Missing IFC test file: {path}");
    }

    /// <summary>A path under <see cref="ProbesDir"/> for a file derived from <paramref name="ifcPath"/>,
    /// with the folder created.</summary>
    public static FilePath ProbePath(FilePath ifcPath, string suffix)
    {
        ProbesDir.Create();
        return ProbesDir.RelativeFile(ifcPath.GetFileNameWithoutExtension() + suffix);
    }

    public static IfcFile LoadStep(FilePath path)
    {
        RequireExists(path);
        return new IfcFile(path, includeGeometry: false);
    }

    public static IfcFile LoadWithOracleGeometry(FilePath path)
    {
        RequireExists(path);
        return new IfcFile(path, includeGeometry: true);
    }
}
