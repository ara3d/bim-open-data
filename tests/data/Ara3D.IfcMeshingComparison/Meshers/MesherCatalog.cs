using Ara3D.IfcMeshingComparison.Harness;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Meshers;

/// <summary>The five meshers the bench compares: two run in process, three are read from dumps that
/// <c>bench/ifc-mesher/meshers/run.mjs</c> wrote (committed for the quick files, under artifacts for
/// the rest). See <c>bench/ifc-mesher/README.md</c>.</summary>
public static class MesherCatalog
{
    public const string WebIfc = "WebIfc";
    public const string IfcLite = "IfcLite";
    public const string IfcOpenShell = "IfcOpenShell";

    public static IReadOnlyList<IMeshingBackend> All { get; } =
    [
        new IfcMesherBackend(),
        new WebIfcBackend(),
        External(WebIfc, "web-ifc 0.0.78 from npm, the engine under ThatOpen components, run in Node (meshers/webifc/mesh.mjs)"),
        External(IfcLite, "IFClite's exact-CSG geometry through the ifclite-geom Python wheel (meshers/ifclite/mesh.py)"),
        External(IfcOpenShell, "IfcOpenShell's geometry iterator in Python (meshers/ifcopenshell/mesh.py)"),
    ];

    /// <summary>The external meshers only: those whose meshes come from dumps.</summary>
    public static IReadOnlyList<DumpBackend> Dumped { get; } = All.OfType<DumpBackend>().ToList();

    public static IReadOnlyList<DirectoryPath> DumpFolders(string mesher)
        => [TestFiles.CommittedDumpsDir.RelativeFolder(mesher), TestFiles.DumpsDir.RelativeFolder(mesher)];

    static DumpBackend External(string name, string description)
        => new(name, description, DumpFolders(name));
}
