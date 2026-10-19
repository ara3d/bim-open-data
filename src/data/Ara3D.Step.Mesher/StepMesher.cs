using Ara3D.Geometry;
using Ara3D.IO.StepParser;
using Ara3D.Models;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher.Plato;
using Ara3D.Utils;

namespace Ara3D.Step.Mesher;

/// <summary>Entry point: a STEP AP203, AP214 or AP242 file as a placed, coloured Model3D, the way Ara3D.Ifc.Mesher's
/// IfcMesher.Build reads IFC. A file the parser cannot finish is meshed as far as it read; a failure of meshing
/// itself comes back as a result whose Success is false.</summary>
public static class StepMesher
{
    /// <summary>The value of <see cref="StepMeshingResult.MesherName"/> on every result built here.</summary>
    public const string Name = "Ara3D.Step.Mesher";

    public static StepMeshingResult Mesh(string path, StepMeshingOptions? options = null)
    {
        StepDocument document;
        try
        {
            document = new StepDocument(new FilePath(path));
        }
        catch (Exception ex)
        {
            return StepMeshingResult.Failed(Name, $"{ex.GetType().Name}: {ex.Message}");
        }
        using (document)
            return Mesh(document, options);
    }

    public static StepMeshingResult Mesh(StepDocument document, StepMeshingOptions? options = null)
    {
        try
        {
            var model = new StepModel(document);
            var diagnostics = new StepMeshingDiagnostics();
            var collector = new StepShapeCollector(model, diagnostics);
            collector.AddProducts();
            collector.AddUnreachedShapes();
            collector.AddUnitContexts();
            options ??= new StepMeshingOptions();
            var builder = new StepModelBuilder(model, MeshWithPlato(model, options, diagnostics), options, diagnostics);
            var built = builder.Build(collector.Placed);
            var placed = collector.Placed;
            return new StepMeshingResult(Name, true, built, built.Meshes.Count, built.Instances.Count, TriangleCount(built),
                built.Instances.Count > 0 ? built.GetBounds() : Bounds3D.Empty, placed, collector.Units.ByContext,
                placed.Count > 0 ? placed.Max(p => p.Depth) : 0, collector.Styles.Count, builder.GeometryPending,
                collector.Cycles, diagnostics, document.Diagnostics, StepMeshingResult.PrefixOf(document), []);
        }
        catch (Exception ex)
        {
            return StepMeshingResult.Failed(Name, $"{ex.GetType().Name}: {ex.Message}", document.Diagnostics);
        }
    }

    /// <summary>The file meshed once by the Plato library, the chordal bound converted to the decoded model's
    /// millimetres; null, recorded, when the library throws before any face is meshed.</summary>
    static StepPlatoMesh? MeshWithPlato(StepModel model, StepMeshingOptions options, StepMeshingDiagnostics diagnostics)
    {
        try
        {
            return model.MeshFile(options.MaxDeviation * MillimetresPerMetre);
        }
        catch (Exception ex)
        {
            diagnostics.Record(nameof(StepPlatoMesher), StepSupport.Unsupported, $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    const double MillimetresPerMetre = 1000;

    static int TriangleCount(Model3D model)
    {
        var count = 0;
        foreach (var i in model.Instances)
            if (i.MeshIndex >= 0)
                count += model.Meshes[i.MeshIndex].FaceIndices.Count;
        return count;
    }
}
