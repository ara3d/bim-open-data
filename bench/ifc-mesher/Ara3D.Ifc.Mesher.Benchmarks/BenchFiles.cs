namespace Ara3D.Ifc.Mesher.Benchmarks;

/// <summary>Finds the benchmarked models and the repository's bench folder.</summary>
public static class BenchFiles
{
    public const string ExtraFoldersVariable = "ARA3D_IFC_TEST_DIRS";

    public static readonly string[] FileNames =
        ["IfcOpenHouse_IFC4.ifc", "example.ifc", "steelplates.ifc", "duplex.ifc"];

    /// <summary>The repository root: the nearest folder above the program holding BimOpenData.sln.</summary>
    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BimOpenData.sln")))
                return dir.FullName;
        throw new InvalidOperationException("BimOpenData.sln not found above " + AppContext.BaseDirectory);
    }

    public static string BenchDir() => Path.Combine(RepoRoot(), "bench", "ifc-mesher");

    /// <summary>The path of each benchmarked file found in data/ or the folders named in ARA3D_IFC_TEST_DIRS; missing files are left out.</summary>
    public static IEnumerable<string> Existing()
    {
        foreach (var name in FileNames)
        {
            var path = Resolve(name);
            if (path != null)
                yield return path;
        }
    }

    public static string? Resolve(string fileName)
        => SearchFolders().Select(f => Path.Combine(f, fileName)).FirstOrDefault(File.Exists);

    static IEnumerable<string> SearchFolders()
    {
        yield return Path.Combine(RepoRoot(), "data");
        var extra = Environment.GetEnvironmentVariable(ExtraFoldersVariable) ?? "";
        foreach (var part in extra.Split([';', Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Directory.Exists(part))
                yield return part;
    }
}
