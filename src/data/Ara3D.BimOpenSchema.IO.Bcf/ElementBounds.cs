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

    /// <summary>Builds a <see cref="BosScene"/> over the data and calls <see cref="FromScene"/>.</summary>
    public static ElementBounds FromBimData(IBimData data)
        => FromScene(new BosScene(data));

    /// <summary>Each element's box is <see cref="BosScene.Bounds"/>: the eight corners of each
    /// visible instance's mesh box, moved by the instance's transform. A caller that keeps the
    /// scene (an MCP session) passes it here to skip building it again.</summary>
    public static ElementBounds FromScene(BosScene scene)
    {
        var boxes = new Dictionary<string, ElementBox>();
        foreach (var entity in scene.ElementsWithGeometry)
        {
            if (scene.GlobalId(entity) is not { } globalId || scene.Bounds(entity) is not { } b)
                continue;
            var box = new ElementBox(b.Min, b.Max);
            boxes[globalId] = boxes.TryGetValue(globalId, out var prior) ? prior.Union(box) : box;
        }
        return new(boxes);
    }
}
