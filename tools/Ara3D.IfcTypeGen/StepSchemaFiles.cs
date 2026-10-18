using Ara3D.Parakeet;
using Ara3D.Utils;

namespace Ara3D.IfcTypeGen;

/// <summary>
/// Finds the STEP long-form EXPRESS schemas under data/express/&lt;edition&gt;/. They state no licence, so they are
/// never committed; data/get-express-schemas.ps1 fetches them.
/// </summary>
public static class StepSchemaFiles
{
    public const string Ap242 = "ap242ed3";
    public const string Ap214 = "ap214ed3";
    public const string Ap203 = "ap203ed2";

    public static readonly DirectoryPath ExpressFolder
        = PathUtil.GetCallerSourceFolder().RelativeFolder("..", "..", "data", "express");

    public const string FetchHint
        = "Fetch the STEP schemas with `pwsh data/get-express-schemas.ps1`, run from deps/bim-open-data.";

    /// <summary>The single .exp file of an edition, or null when it has not been fetched.</summary>
    public static string? Find(string edition)
    {
        var folder = Path.Combine(ExpressFolder, edition);
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.exp", SearchOption.AllDirectories) : [];
        return files.Length switch
        {
            0 => null,
            1 => files[0],
            _ => throw new InvalidOperationException($"{folder} holds {files.Length} .exp files; expected one"),
        };
    }

    public static ParserInput Load(string edition)
        => ParserInput.FromFile(Find(edition)
            ?? throw new FileNotFoundException($"No EXPRESS schema for {edition} in {ExpressFolder}. {FetchHint}"));
}
