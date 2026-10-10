using Ara3D.Utils;

namespace Ara3D.BimOpenSchema.IO.Usd;

/// <summary>
/// Writes the BOS geometry tables as USD: materials as UsdPreviewSurface shaders, each mesh
/// once as a prototype, and each instance as an instanceable prim that references its
/// prototype, binds its material, and carries its transform.
/// </summary>
internal static class UsdGeometry
{
    private const string MaterialBindingApi = "prepend apiSchemas = [\"MaterialBindingAPI\"]";

    /// <summary>/Model/Materials/M{i}, one per row of the BOS material table. Colours are the
    /// BOS bytes divided by 255, written as given: BOS does not say whether they are sRGB.</summary>
    public static int WriteMaterials(UsdaWriter w, BimGeometry g)
    {
        var count = g.GetNumMaterials();
        w.Line().Text("def Scope \"").Text(UsdNames.MaterialsScope).Text('"').End().Open();
        for (var i = 0; i < count; i++)
        {
            w.Line().Text("def Material \"").Text(UsdNames.MaterialPrefix).Int(i).Text('"').End().Open();
            w.Line().Text("token outputs:surface.connect = <").Text(UsdNames.MaterialPath).Int(i).Text("/Surface.outputs:surface>").End();
            w.Line("def Shader \"Surface\"").Open();
            w.Line("uniform token info:id = \"UsdPreviewSurface\"");
            w.Line().Text("color3f inputs:diffuseColor = ").Float3(
                g.MaterialRed[i].ToNormalizedFloat(),
                g.MaterialGreen[i].ToNormalizedFloat(),
                g.MaterialBlue[i].ToNormalizedFloat()).End();
            w.Line().Text("float inputs:metallic = ").Float(g.MaterialMetallic[i].ToNormalizedFloat()).End();
            w.Line().Text("float inputs:opacity = ").Float(g.MaterialAlpha[i].ToNormalizedFloat()).End();
            w.Line().Text("float inputs:roughness = ").Float(g.MaterialRoughness[i].ToNormalizedFloat()).End();
            w.Line("token outputs:surface");
            w.Close();
            w.Close();
        }
        w.Close();
        return count;
    }

    /// <summary>/Model/Prototypes/P{i}/Mesh, one per row of the BOS mesh table, under an
    /// abstract ("class") scope so the prototypes themselves are not drawn. Instancing in USD
    /// shares a prim's descendants, not the prim, so each mesh sits one level below its
    /// prototype root.</summary>
    public static int WritePrototypes(UsdaWriter w, BimGeometry g)
    {
        var count = g.GetNumMeshes();
        w.Line().Text("class Scope \"").Text(UsdNames.PrototypesScope).Text('"').End().Open();
        for (var i = 0; i < count; i++)
        {
            w.Line().Text("def Xform \"").Text(UsdNames.PrototypePrefix).Int(i).Text('"').End().Open();
            w.Line().Text("def Mesh \"").Text(UsdNames.PrototypeMesh).Text('"').End().Open();
            WriteMeshBody(w, g, g.GetMeshSlice(i));
            w.Close();
            w.Close();
        }
        w.Close();
        return count;
    }

    private static void WriteMeshBody(UsdaWriter w, BimGeometry g, Ara3D.Models.MeshSliceStruct slice)
    {
        var firstVertex = slice.BaseVertex;
        var vertexCount = slice.VertexCount;
        var firstIndex = (int)slice.FirstIndex;
        var faceCount = (int)slice.IndexCount / 3;

        // BIM meshes are not reliably closed or consistently wound; draw both sides.
        w.Line("uniform bool doubleSided = 1");
        if (vertexCount > 0)
            WriteExtent(w, g, firstVertex, vertexCount);

        w.Line().Text("int[] faceVertexCounts = [");
        for (var f = 0; f < faceCount; f++)
            w.Text(f == 0 ? "3" : ", 3");
        w.Text(']').End();

        w.Line().Text("int[] faceVertexIndices = [");
        for (var k = 0; k < faceCount * 3; k++)
        {
            if (k > 0) w.Text(", ");
            w.Int(g.IndexBuffer[firstIndex + k]);
        }
        w.Text(']').End();

        w.Line().Text("point3f[] points = [");
        for (var v = firstVertex; v < firstVertex + vertexCount; v++)
        {
            if (v > firstVertex) w.Text(", ");
            w.Text('(').Fixed4(g.VertexX[v]).Text(", ").Fixed4(g.VertexY[v]).Text(", ").Fixed4(g.VertexZ[v]).Text(')');
        }
        w.Text(']').End();
        w.Line("uniform token subdivisionScheme = \"none\"");
    }

    private static void WriteExtent(UsdaWriter w, BimGeometry g, int firstVertex, int vertexCount)
    {
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
        for (var v = firstVertex; v < firstVertex + vertexCount; v++)
        {
            minX = Math.Min(minX, g.VertexX[v]); maxX = Math.Max(maxX, g.VertexX[v]);
            minY = Math.Min(minY, g.VertexY[v]); maxY = Math.Max(maxY, g.VertexY[v]);
            minZ = Math.Min(minZ, g.VertexZ[v]); maxZ = Math.Max(maxZ, g.VertexZ[v]);
        }
        w.Line().Text("float3[] extent = [(")
            .Fixed4(minX).Text(", ").Fixed4(minY).Text(", ").Fixed4(minZ).Text("), (")
            .Fixed4(maxX).Text(", ").Fixed4(maxY).Text(", ").Fixed4(maxZ).Text(")]").End();
    }

    /// <summary>True when the instance's mesh index names a row of the mesh table; an
    /// instance without one has nothing to draw and is not written.</summary>
    public static bool HasMesh(BimGeometry g, int instance)
        => (uint)g.InstanceMeshIndex[instance] < (uint)g.GetNumMeshes();

    /// <summary>The prim I{instance}: instanceable, referencing its mesh's prototype, bound to
    /// its material when it has one, hidden when its BOS flag says so, and transformed.</summary>
    public static void WriteInstance(UsdaWriter w, BimGeometry g, int instance)
    {
        var material = g.InstanceMaterialIndex[instance];
        var hasMaterial = (uint)material < (uint)g.GetNumMaterials();

        w.Line().Text("def Xform \"").Text(UsdNames.InstancePrefix).Int(instance).Text('"').OpenMetadata();
        w.Line("instanceable = true");
        if (hasMaterial)
            w.Line(MaterialBindingApi);
        w.Line().Text("prepend references = ").PathRef(UsdNames.PrototypePath, g.InstanceMeshIndex[instance]).End();
        w.CloseMetadata().Open();
        if (hasMaterial)
            w.Line().Text("rel material:binding = ").PathRef(UsdNames.MaterialPath, material).End();
        if (IsHidden(g, instance))
            w.Line("token visibility = \"invisible\"");
        WriteTransform(w, g, g.InstanceTransformIndex[instance]);
        w.Close();
    }

    private static bool IsHidden(BimGeometry g, int instance)
        => instance < g.InstanceFlags.Length
           && (g.InstanceFlags[instance] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0;

    /// <summary>BOS composes scale, then rotation, then translation, which is USD's
    /// translate-orient-scale op order. Ops equal to the identity are left out; a missing
    /// transform row leaves the instance where its mesh is.</summary>
    private static void WriteTransform(UsdaWriter w, BimGeometry g, int t)
    {
        if ((uint)t >= (uint)g.GetNumTransforms())
            return;
        var translate = g.TransformTX[t] != 0 || g.TransformTY[t] != 0 || g.TransformTZ[t] != 0;
        var orient = g.TransformQX[t] != 0 || g.TransformQY[t] != 0 || g.TransformQZ[t] != 0 || g.TransformQW[t] != 1;
        var scale = g.TransformSX[t] != 1 || g.TransformSY[t] != 1 || g.TransformSZ[t] != 1;
        if (!translate && !orient && !scale)
            return;

        if (translate)
            w.Line().Text("float3 xformOp:translate = ").Float3(g.TransformTX[t], g.TransformTY[t], g.TransformTZ[t]).End();
        if (orient)
            // USD writes a quaternion real part first: (w, x, y, z).
            w.Line().Text("quatf xformOp:orient = (").Float(g.TransformQW[t]).Text(", ").Float(g.TransformQX[t])
                .Text(", ").Float(g.TransformQY[t]).Text(", ").Float(g.TransformQZ[t]).Text(')').End();
        if (scale)
            w.Line().Text("float3 xformOp:scale = ").Float3(g.TransformSX[t], g.TransformSY[t], g.TransformSZ[t]).End();

        w.Line().Text("uniform token[] xformOpOrder = [");
        var first = true;
        foreach (var (authored, op) in new[] { (translate, "translate"), (orient, "orient"), (scale, "scale") })
        {
            if (!authored) continue;
            w.Text(first ? "\"xformOp:" : ", \"xformOp:").Text(op).Text('"');
            first = false;
        }
        w.Text(']').End();
    }
}
