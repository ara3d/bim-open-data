using Ara3D.Step.Entities;
using Ara3D.Step.Mesher.Plato;
using Ara3D.StepBrep;

namespace Ara3D.Step.Mesher;

public static class StepShapeMesher
{
    /// <summary>The mesh of one geometric representation item, taken from its file meshed once by
    /// <see cref="StepPlatoMesher.MeshFile"/>: the body decoded from the item at its first occurrence, in the item's own
    /// coordinates, lengths in metres by <paramref name="units"/>. Placement, mapped_item, assemblies and styles are the
    /// caller's. Returns null, recorded as unsupported, when the decoder read no body from the item. Each face is
    /// recorded under its surface's entity name: supported when meshed, unsupported with a dropped face otherwise.</summary>
    public static StepShapeMesh? MeshItem(this StepPlatoMesh file, StepModel model, StepEntity item, StepUnits units,
        StepMeshingDiagnostics diagnostics)
    {
        if (!file.BodyOfInstance.TryGetValue(item.Id, out var body))
        {
            diagnostics.Record(item.EntityName(), StepSupport.Unsupported, $"#{item.Id}: no B-rep body decoded from this item");
            return null;
        }
        foreach (var face in file.FacesOf(body))
            file.RecordFace(model, face, diagnostics);
        return file.ToShapeMesh(body, units);
    }

    static void RecordFace(this StepPlatoMesh file, StepModel model, BrepFaceIndex face, StepMeshingDiagnostics diagnostics)
    {
        var surface = model.Get(file.SurfaceInstance(face)).EntityName();
        if (file.IsMeshed(face))
        {
            diagnostics.Record(surface, StepSupport.Supported);
            return;
        }
        var reason = string.Join("; ", file.Outcomes[face.Value].Diagnostics
            .Where(d => (bool)d.Severity.IsError()).Select(d => $"{(string)d.Code}: {(string)d.Message}"));
        diagnostics.Record(surface, StepSupport.Unsupported, $"face #{file.FaceInstance(face)}: {reason}");
        diagnostics.RecordDroppedFace(file.FaceInstance(face), reason);
    }
}
