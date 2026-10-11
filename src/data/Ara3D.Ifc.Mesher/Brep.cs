using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;

namespace Ara3D.Ifc.Mesher;

/// <summary>
/// Faceted and advanced breps, face-based and shell-based surface models, and single faces:
/// each is a set of faces that <see cref="FaceSetBuilder"/> triangulates. Open shells produce
/// single-sided faces.
/// </summary>
public static class Brep
{
    public static TriangleMesh3D BuildFacetedBrep(MeshingContext ctx, IfcEntity brep)
    {
        ctx.Diagnostics.RecordSupported("IFCFACETEDBREP");
        var shell = MeshHelpers.ResolveRequired(ctx, brep, IfcManifoldSolidBrep.Instance.Outer);
        return BuildShell(ctx, shell);
    }

    /// <summary>Advanced brep: faces on planar, cylindrical, revolved and B-spline surfaces follow
    /// their surface (see <see cref="SurfaceMap"/>); any other surface is flattened to its bounds.</summary>
    public static TriangleMesh3D BuildAdvancedBrep(MeshingContext ctx, IfcEntity brep)
    {
        ctx.Diagnostics.RecordApproximate("IFCADVANCEDBREP", "Bounds only; curved advanced-face surfaces ignored");
        var shell = MeshHelpers.ResolveRequired(ctx, brep, IfcManifoldSolidBrep.Instance.Outer);
        return BuildShell(ctx, shell);
    }

    public static TriangleMesh3D BuildFaceBasedSurfaceModel(MeshingContext ctx, IfcEntity model)
    {
        ctx.Diagnostics.RecordSupported("IFCFACEBASEDSURFACEMODEL");
        var meshes = MeshHelpers.ReadIds(model, IfcFaceBasedSurfaceModel.Instance.FbsmFaces)
            .Select(id => BuildFaceBasedSurfaceElement(ctx, ctx.GetEntity(id)))
            .ToList();
        return MeshHelpers.Merge(meshes);
    }

    public static TriangleMesh3D BuildFaceBasedSurfaceElement(MeshingContext ctx, IfcEntity element)
        => element.GetEntityName() switch
        {
            "IFCCONNECTEDFACESET" => BuildConnectedFaceSet(ctx, element),
            "IFCOPENSHELL" or "IFCOPENEDSHELL" or "IFCCLOSEDSHELL" => BuildShell(ctx, element),
            _ => BuildConnectedFaceSet(ctx, element),
        };

    /// <summary>Unwraps CfsFaces to IFCFACE entities and triangulates each face.</summary>
    public static TriangleMesh3D BuildConnectedFaceSet(MeshingContext ctx, IfcEntity faceSet)
    {
        ctx.Diagnostics.RecordSupported("IFCCONNECTEDFACESET");
        return FaceSetBuilder.Build(ctx, MeshHelpers.ReadIds(faceSet, IfcConnectedFaceSet.Instance.CfsFaces));
    }

    public static TriangleMesh3D BuildShellBasedSurfaceModel(MeshingContext ctx, IfcEntity model)
    {
        ctx.Diagnostics.RecordSupported("IFCSHELLBASEDSURFACEMODEL");
        var meshes = MeshHelpers.ReadIds(model, IfcShellBasedSurfaceModel.Instance.SbsmBoundary)
            .Select(id => BuildShell(ctx, ctx.GetEntity(id)))
            .ToList();
        return MeshHelpers.Merge(meshes);
    }

    public static TriangleMesh3D BuildSingleFace(MeshingContext ctx, IfcEntity face)
    {
        var name = face.GetEntityName();
        ctx.Diagnostics.RecordApproximate(name, "Single face via polyloop bounds");
        return FaceSetBuilder.Build(ctx, [face.Id]);
    }

    static TriangleMesh3D BuildShell(MeshingContext ctx, IfcEntity shell)
    {
        var faceIds = shell.GetEntityName() switch
        {
            "IFCCLOSEDSHELL" => MeshHelpers.ReadIds(shell, IfcClosedShell.Instance.CfsFaces),
            "IFCOPENSHELL" or "IFCOPENEDSHELL" => MeshHelpers.ReadIds(shell, IfcOpenShell.Instance.CfsFaces),
            _ => throw new NotSupportedException($"Unsupported shell {shell.GetEntityName()}"),
        };
        return FaceSetBuilder.Build(ctx, faceIds);
    }
}
