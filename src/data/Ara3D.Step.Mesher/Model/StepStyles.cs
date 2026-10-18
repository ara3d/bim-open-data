using Ara3D.Geometry;
using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>
/// The colours styled_items give, by the id of the item they style (a solid, a shell, a face, a mapped_item or a
/// representation), and the one colour a shape item takes: its own, else its shells', else the most common colour
/// of its faces. A shape whose faces carry several colours is recorded as approximate: Model3D gives an instance one
/// material, so per-face colour waits on a mesh split by colour.
/// </summary>
public sealed class StepStyles
{
    readonly Dictionary<int, Color> _byTarget = new();
    readonly Dictionary<int, Color?> _byShape = new();
    readonly StepMeshingDiagnostics _diagnostics;

    public StepStyles(StepModel model, IReadOnlyList<int> styledItemIds, StepMeshingDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
        foreach (var id in styledItemIds)
        {
            var styled = model.Get(id);
            diagnostics.Guard(styled, () => Add(styled));
        }
    }

    /// <summary>The number of styled instances with a surface colour.</summary>
    public int Count => _byTarget.Count;

    /// <summary>The colour styled directly on an instance, or null.</summary>
    public Color? Of(int id)
        => _byTarget.TryGetValue(id, out var c) ? c : null;

    /// <summary>The colour of a shape item, read once per item; null when nothing styles it or its topology.</summary>
    public Color? OfShape(StepEntity item)
    {
        if (_byShape.TryGetValue(item.Id, out var cached))
            return cached;
        Color? colour = null;
        _diagnostics.Guard(item, () => colour = Of(item.Id) ?? FromShells(item));
        return _byShape[item.Id] = colour;
    }

    void Add(StepEntity styled)
    {
        if (styled.Ref(StyledItem.Instance.Item) is { } target && styled.SurfaceColour() is { } colour)
            _byTarget.TryAdd(target.Id, colour);
    }

    Color? FromShells(StepEntity item)
    {
        var shells = Shells(item);
        foreach (var shell in shells)
            if (Of(shell.Id) is { } c)
                return c;
        var counts = new Dictionary<Color, int>();
        foreach (var shell in shells)
            foreach (var face in shell.Refs(ConnectedFaceSet.Instance.CfsFaces) ?? [])
                if (Of(face.Id) is { } c)
                    counts[c] = counts.GetValueOrDefault(c) + 1;
        if (counts.Count == 0)
            return null;
        if (counts.Count > 1)
            _diagnostics.Record(item.EntityName(), StepSupport.Approximate,
                $"#{item.Id}: faces carry {counts.Count} colours; the most common is used for the whole item");
        return counts.MaxBy(kv => kv.Value).Key;
    }

    static IReadOnlyList<StepEntity> Shells(StepEntity item)
        => item.Is<IManifoldSolidBrep>() ? (item.Ref(ManifoldSolidBrep.Instance.Outer) is { } outer ? [outer] : [])
            : item.Is<IShellBasedSurfaceModel>() ? item.Refs(ShellBasedSurfaceModel.Instance.SbsmBoundary) ?? []
            : item.Is<IFaceBasedSurfaceModel>() ? item.Refs(FaceBasedSurfaceModel.Instance.FbsmFaces) ?? []
            : item.Is<IConnectedFaceSet>() ? [item]
            : [];
}
