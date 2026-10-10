using System.Numerics;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>An axis-aligned box in model coordinates: BOS geometry's z-up metres, which are
/// the IFC file's own coordinates and so also BCF's.</summary>
public readonly record struct ElementBox(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    public ElementBox Union(ElementBox other)
        => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    public static ElementBox Of(IEnumerable<Vector3> points)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new(min, max);
    }
}

/// <summary>The world-space bounding box of every element with visible geometry in a BOS
/// model, keyed by GlobalId. An element with no instance, or only hidden ones, has no box:
/// <see cref="TryGet"/> says so rather than guessing a place. When several entities share a
/// GlobalId (a federated model holds one per source model), their boxes are united.</summary>
public sealed class ElementBounds
{
    private readonly Dictionary<string, ElementBox> _boxes;

    private ElementBounds(Dictionary<string, ElementBox> boxes)
        => _boxes = boxes;

    /// <summary>The number of GlobalIds with a box.</summary>
    public int Count => _boxes.Count;

    public bool TryGet(string globalId, out ElementBox box)
        => _boxes.TryGetValue(globalId, out box);

    /// <summary>The box of every listed element that has one, or null when none does.</summary>
    public ElementBox? Union(IEnumerable<string> globalIds)
    {
        ElementBox? result = null;
        foreach (var id in globalIds)
            if (_boxes.TryGetValue(id, out var box))
                result = result?.Union(box) ?? box;
        return result;
    }

    /// <summary>Bounds each instance by transforming the eight corners of its mesh's local box,
    /// which contains the transformed mesh and costs one pass over the vertices plus eight
    /// points per instance.</summary>
    public static ElementBounds FromBimData(IBimData data)
    {
        var boxes = new Dictionary<string, ElementBox>();
        var geometry = data.Geometry;
        if (geometry is null)
            return new(boxes);

        var meshBoxes = new ElementBox?[geometry.MeshVertexOffset.Length];
        for (var i = 0; i < geometry.InstanceEntityIndex.Length; i++)
        {
            var entity = geometry.InstanceEntityIndex[i];
            if (entity < 0 || entity >= data.Entities.Length || IsHidden(geometry, i))
                continue;
            var globalId = GlobalIdOf(data, entity);
            if (globalId.Length == 0)
                continue;
            var mesh = geometry.InstanceMeshIndex[i];
            if (mesh < 0 || mesh >= meshBoxes.Length)
                continue;
            var local = meshBoxes[mesh] ??= MeshBox(geometry, mesh);
            if (local.Min.X > local.Max.X)
                continue;
            var world = ElementBox.Of(Corners(local).Select(c => Transform(geometry, geometry.InstanceTransformIndex[i], c)));
            boxes[globalId] = boxes.TryGetValue(globalId, out var prior) ? prior.Union(world) : world;
        }
        return new(boxes);
    }

    private static bool IsHidden(BimGeometry g, int instance)
        => instance < g.InstanceFlags.Length
           && (g.InstanceFlags[instance] & (byte)BimGeometry.InstanceFlagEnum.IsHidden) != 0;

    private static string GlobalIdOf(IBimData data, int entity)
    {
        var index = (int)data.Entities[entity].GlobalId;
        return index >= 0 && index < data.Strings.Length ? data.Strings[index] ?? "" : "";
    }

    /// <summary>The box of a mesh's vertices in its own coordinates; an empty mesh gives an
    /// inverted box, which the caller skips.</summary>
    private static ElementBox MeshBox(BimGeometry g, int mesh)
    {
        var start = g.MeshVertexOffset[mesh];
        var end = mesh + 1 < g.MeshVertexOffset.Length ? g.MeshVertexOffset[mesh + 1] : g.VertexX.Length;
        return ElementBox.Of(Enumerable.Range(start, end - start).Select(v => new Vector3(g.VertexX[v], g.VertexY[v], g.VertexZ[v]) / BimGeometry.VertexMultiplier));
    }

    private static IEnumerable<Vector3> Corners(ElementBox b)
    {
        for (var i = 0; i < 8; i++)
            yield return new((i & 1) == 0 ? b.Min.X : b.Max.X, (i & 2) == 0 ? b.Min.Y : b.Max.Y, (i & 4) == 0 ? b.Min.Z : b.Max.Z);
    }

    /// <summary>BOS composes scale, then rotation, then translation (row-vector
    /// Scale * Rotation * Translation, as <c>BimGeometryExtensions.GetTransformMatrix</c> does).</summary>
    private static Vector3 Transform(BimGeometry g, int t, Vector3 p)
    {
        var scaled = p * new Vector3(g.TransformSX[t], g.TransformSY[t], g.TransformSZ[t]);
        var rotation = new Quaternion(g.TransformQX[t], g.TransformQY[t], g.TransformQZ[t], g.TransformQW[t]);
        return Vector3.Transform(scaled, rotation) + new Vector3(g.TransformTX[t], g.TransformTY[t], g.TransformTZ[t]);
    }
}
