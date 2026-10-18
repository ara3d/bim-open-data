using Ara3D.Step.Entities;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>The STEP entities that become a row of one of the ten kernel tables when an occurrence reaches them.</summary>
public static class KernelKinds
{
    public static bool IsKernelRow(StepEntity e)
        => e.Is<ST.IVertexPoint>() || e.Is<ST.IEdgeCurve>() || e.Is<ST.ILoop>() || e.Is<ST.IFaceSurface>()
            || (e.Is<ST.IConnectedFaceSet>() && !e.Is<ST.IOrientedClosedShell>() && !e.Is<ST.IOrientedOpenShell>())
            || e.Is<ST.IManifoldSolidBrep>() || e.Is<ST.IShellBasedSurfaceModel>()
            || e.Is<ST.ISurface>() || e.Is<ST.IPcurve>()
            || (e.Is<ST.ICurve>() && !e.Is<ST.IPcurve>());
}
