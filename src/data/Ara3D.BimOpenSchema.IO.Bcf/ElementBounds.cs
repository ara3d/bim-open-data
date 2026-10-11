using System.Numerics;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>The world-space bounding box of every element with visible geometry in a BOS
/// model, keyed by GlobalId. An element with no instance, or only hidden ones, has no box:
/// <see cref="TryGet"/> says so rather than guessing a place. When several entities share a
/// GlobalId (a federated model holds one per source model), their boxes are united.</summary>
public sealed class ElementBounds
{
    private readonly Dictionary<string, BosBox> _boxes;

    private ElementBounds(Dictionary<string, BosBox> boxes)
        => _boxes = boxes;

    /// <summary>The number of GlobalIds with a box.</summary>
    public int Count => _boxes.Count;

    public bool TryGet(string globalId, out BosBox box)
        => _boxes.TryGetValue(globalId, out box);

    /// <summary>The box of every listed element that has one, or null when none does.</summary>
    public BosBox? Union(IEnumerable<string> globalIds)
    {
        BosBox? result = null;
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
        var boxes = new Dictionary<string, BosBox>();
        foreach (var entity in scene.ElementsWithGeometry)
        {
            if (scene.GlobalId(entity) is not { } globalId || scene.Bounds(entity) is not { } b)
                continue;
            boxes[globalId] = boxes.TryGetValue(globalId, out var prior) ? prior.Union(b) : b;
        }
        return new(boxes);
    }
}
