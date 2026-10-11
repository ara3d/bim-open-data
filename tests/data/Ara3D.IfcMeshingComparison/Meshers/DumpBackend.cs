using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Meshers;

/// <summary>A mesher that ran elsewhere: its meshes are read from a <see cref="MeshDump"/> found in the
/// first of <paramref name="folders"/> that has one for the model. Absent dump, <see cref="Build"/>
/// fails with <see cref="NoDump"/>, which the comparison tests turn into an ignore.</summary>
public sealed class DumpBackend(string name, string description, IReadOnlyList<DirectoryPath> folders) : IMeshingBackend
{
    public const string NoDump = "no dump";

    public string Name => name;
    public string Description => description;

    public FilePath? FindDump(FilePath ifcPath)
    {
        foreach (var folder in folders)
        {
            var candidate = MeshDump.PathFor(folder, ifcPath);
            if (candidate.Exists())
                return candidate;
        }
        return null;
    }

    public MeshingResult Build(FilePath ifcPath)
    {
        var dump = FindDump(ifcPath);
        if (dump is null)
            return MeshingResult.Failed(Name, ifcPath, 0, NoDump);
        try
        {
            var (header, model) = MeshDump.Read(dump.Value);
            return MeshingResult.FromModel(Name, ifcPath, header.ElapsedMs, model);
        }
        catch (Exception ex)
        {
            return MeshingResult.Failed(Name, ifcPath, 0, ex.Message);
        }
    }
}
