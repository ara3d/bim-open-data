using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Triangulates a set of IFCFACE entities into one welded mesh. Each face's bounds are read by
/// <see cref="FaceBounds"/> and its surface by <see cref="FaceSurfaceMaps"/>; the kernel's
/// <see cref="FaceMesher"/> does the rest. A face that cannot be triangulated is left out and
/// recorded against the current product.
/// </summary>
internal static class FaceSetBuilder
{
    public static TriangleMesh3D Build(MeshingContext ctx, IReadOnlyList<int> faceIds)
    {
        var mesh = new WeldedMeshBuilder();
        foreach (var faceId in faceIds)
        {
            var face = ctx.GetEntity(faceId);
            RecordFaceDiagnostics(ctx, face);
            var (outer, holes, sameSense) = FaceBounds.Read(ctx, face);
            var drop = outer.Count < 3
                ? new FaceDrop(FaceDropKind.TooFewBoundaryPoints)
                : mesh.AddFace(FaceSurfaceMaps.Resolve(ctx, face, outer), outer, holes, sameSense);
            if (drop is { } d)
                ctx.RecordDroppedFace(faceId, d.Message);
        }
        return mesh.ToMesh();
    }

    static void RecordFaceDiagnostics(MeshingContext ctx, IfcEntity face)
    {
        var name = face.GetEntityName();
        if (name is "IFCADVANCEDFACE" or "IFCFACESURFACE")
        {
            var surface = MeshHelpers.ResolveOptional(ctx, face, IfcFaceSurface.Instance.FaceSurface);
            if (surface?.GetEntityName() is "IFCPLANE" or "IFCCURVEBOUNDEDPLANE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Planar advanced face via edge-loop bounds");
            }
            else if (surface?.GetEntityName() is "IFCCYLINDRICALSURFACE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Cylindrical advanced face tessellated in surface parameter space");
            }
            else if (surface?.GetEntityName() is "IFCSURFACEOFREVOLUTION")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "Surface-of-revolution advanced face tessellated in (angle, meridian) space");
            }
            else if (surface?.GetEntityName() is "IFCBSPLINESURFACEWITHKNOTS" or "IFCRATIONALBSPLINESURFACEWITHKNOTS" or "IFCBSPLINESURFACE")
            {
                ctx.Diagnostics.RecordSupported(surface.GetEntityName());
                ctx.Diagnostics.RecordApproximate(name, "B-spline advanced face tessellated over its (u,v) control-net parameter grid");
            }
            else
                ctx.Diagnostics.RecordApproximate(name, "Bounds only; curved advanced-face surfaces ignored");
        }
    }
}
