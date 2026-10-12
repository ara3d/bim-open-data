using Ara3D.Geometry;
using Ara3D.IfcLoader;
using Ara3D.IfcTypes;
using Ara3D.IO.StepParser;

namespace Ara3D.Ifc.Mesher;

public static class MeshHelpers
{
    public static IfcEntity ResolveRequired(MeshingContext ctx, IfcEntity entity, IfcAttribute attribute)
    {
        var id = ReadOptionalId(entity, attribute);
        if (id is null)
            throw new InvalidOperationException($"{entity.GetEntityName()} #{entity.Id} missing {attribute.Name}");
        return ctx.GetEntity(id.Value);
    }

    public static IfcEntity? ResolveOptional(MeshingContext ctx, IfcEntity entity, IfcAttribute attribute)
    {
        var id = ReadOptionalId(entity, attribute);
        return id is null ? null : ctx.GetEntityOrDefault(id.Value);
    }

    public static int? ReadOptionalId(IfcEntity entity, IfcAttribute attribute)
    {
        var token = entity.GetValue(attribute.Index);
        return token.IsId ? token.AsId() : null;
    }

    public static double ReadNumber(IfcEntity entity, IfcAttribute attribute)
        => ReadNumberByIndex(entity, attribute.Index);

    public static double ReadNumberByIndex(IfcEntity entity, int index)
    {
        var token = entity.GetValue(index);
        return token.IsNumber ? token.AsNumber() : 0.0;
    }

    public static int? ReadOptionalIdByIndex(IfcEntity entity, int index)
    {
        var token = entity.GetValue(index);
        return token.IsId ? token.AsId() : null;
    }

    public static bool ReadOptionalBool(IfcEntity entity, IfcAttribute attribute, bool defaultValue = true)
    {
        var token = entity.GetValue(attribute.Index);
        if (token.IsUnassignedOrRedeclared)
            return defaultValue;
        var text = token.IsString ? token.AsString() : token.ToString();
        if (text.Contains(".F.", StringComparison.Ordinal))
            return false;
        if (text.Contains(".T.", StringComparison.Ordinal))
            return true;
        return defaultValue;
    }

    public static IReadOnlyList<int> ReadIds(IfcEntity entity, IfcAttribute attribute)
        => ReadIdsByIndex(entity, attribute.Index);

    public static IReadOnlyList<int> ReadIdsByIndex(IfcEntity entity, int index)
    {
        var token = entity.GetValue(index);
        if (!token.IsList)
            return [];
        return token.AsList(entity.Document).Where(t => t.IsId).Select(t => t.AsId()).ToList();
    }

    public static IReadOnlyList<double> ReadNumbers(IfcEntity entity, IfcAttribute attribute)
    {
        var token = entity.GetValue(attribute.Index);
        if (!token.IsList)
            return [];
        return token.AsList(entity.Document).Where(t => t.IsNumber).Select(t => t.AsNumber()).ToList();
    }

    // Forwarders into Ara3D.MeshKernel, removed in B12/B13 once callers switch.
    public static TriangleMesh3D Merge(IReadOnlyList<TriangleMesh3D> meshes) => MeshOps.Merge(meshes);

    public static TriangleMesh3D Transform(TriangleMesh3D mesh, Matrix4x4 matrix) => MeshOps.Transform(mesh, matrix);

    public static float LinearDeterminant(Matrix4x4 m) => MeshOps.LinearDeterminant(m);

    public static Bounds3D GetBounds(TriangleMesh3D mesh) => MeshMeasure.Bounds(mesh);

    public static double SignedVolume(TriangleMesh3D mesh) => MeshMeasure.SignedVolume(mesh);
}
