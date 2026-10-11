using System.Diagnostics;
using Ara3D.Ifc.Mesher;
using Ara3D.IfcLoader;
using Ara3D.Utils;

namespace Ara3D.IfcMeshingComparison.Meshers;

/// <summary>The pure C# mesher, <see cref="ModelAssembler"/>, over a file opened without web-ifc geometry.</summary>
public sealed class IfcMesherBackend : IMeshingBackend
{
    public string Name => IfcMesher.Name;
    public string Description =>
        "Pure C# meshing: IfcFile(includeGeometry:false) + ModelAssembler (Ara3D.Ifc.Mesher).";

    public MeshingResult Build(FilePath ifcPath)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var file = new IfcFile(ifcPath, includeGeometry: false);
            var (model, diagnostics) = ModelAssembler.BuildModel(file.EntityResolver);
            sw.Stop();
            return MeshingResult.FromModel(Name, ifcPath, sw.ElapsedMilliseconds, model, diagnostics);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return MeshingResult.Failed(Name, ifcPath, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
