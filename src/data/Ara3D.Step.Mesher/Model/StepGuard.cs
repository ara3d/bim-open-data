using Ara3D.Step.Entities;

namespace Ara3D.Step.Mesher;

/// <summary>Turns a malformed instance (a dangling reference, a value of the wrong kind) into a diagnostic, so
/// one bad instance costs its own geometry and never the file's.</summary>
public static class StepGuard
{
    public static void Guard(this StepMeshingDiagnostics diagnostics, StepEntity e, Action read)
    {
        try
        {
            read();
        }
        catch (Exception ex)
        {
            diagnostics.Record(e.EntityName(), StepSupport.Unsupported, $"#{e.Id}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
