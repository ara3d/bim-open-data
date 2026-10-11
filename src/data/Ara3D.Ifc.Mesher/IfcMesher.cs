using Ara3D.IfcLoader;
using Ara3D.IO.StepParser;
using Ara3D.Utils;

namespace Ara3D.Ifc.Mesher;

/// <summary>Entry point: meshes every product of an IFC file from its STEP geometry definitions,
/// in C#, with no native code. A failure comes back as a result whose <c>Success</c> is false.</summary>
public static class IfcMesher
{
    /// <summary>The value of <see cref="IfcMeshingResult.MesherName"/> on every result built here.</summary>
    public const string Name = "Ara3D.Ifc.Mesher";

    public static IfcMeshingResult Build(IfcEntityResolver resolver)
    {
        try
        {
            var (model, diagnostics) = ModelAssembler.BuildModel(resolver);
            return IfcMeshingResult.FromModel(Name, model, diagnostics.Messages);
        }
        catch (Exception ex)
        {
            return IfcMeshingResult.Failed(Name, ex.Message);
        }
    }

    public static IfcMeshingResult Build(StepDocument document)
        => Build(new IfcEntityResolver(document));

    public static IfcMeshingResult Build(FilePath ifcPath)
    {
        using var document = new StepDocument(ifcPath);
        return Build(document);
    }
}
