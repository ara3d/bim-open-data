using Ara3D.Step.Entities;

namespace Ara3D.Step.Mesher;

public static class StepShapeMesher
{
    /// <summary>Meshes one geometric representation item of a shape representation (manifold_solid_brep and its
    /// subtypes, brep_with_voids, shell_based_surface_model, face_based_surface_model, and the shells and faces they
    /// hold) in the item's own coordinates, lengths scaled to metres by <paramref name="units"/>. Placement,
    /// mapped_item, assemblies and styles are the caller's. Returns null when the item is not a meshable shape;
    /// records every unsupported entity and dropped face in <paramref name="diagnostics"/>.</summary>
    public static StepShapeMesh? MeshItem(this StepModel model, StepEntity item, StepUnits units,
        StepMeshingDiagnostics diagnostics, float maxDeviation = MeshKernel.Tolerance.ChordDeviation)
        => throw new NotImplementedException("TKT-305 part A");
}
