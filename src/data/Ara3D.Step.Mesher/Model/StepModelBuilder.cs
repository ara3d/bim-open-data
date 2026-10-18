using Ara3D.Models;
using Ara3D.Step.Entities;

namespace Ara3D.Step.Mesher;

/// <summary>Meshes each placed item once, through <see cref="StepShapeMesher.MeshItem"/>, and adds one instance per
/// placement, sharing the item's mesh. An item whose mesher throws NotImplementedException counts as geometry
/// pending; any other exception is recorded against the item.</summary>
public sealed class StepModelBuilder
{
    readonly StepModel _model;
    readonly StepMeshingOptions _options;
    readonly StepMeshingDiagnostics _diagnostics;
    readonly Dictionary<int, int?> _meshOfItem = new();
    readonly Model3DBuilder _builder = new();

    public StepModelBuilder(StepModel model, StepMeshingOptions options, StepMeshingDiagnostics diagnostics)
    {
        _model = model;
        _options = options;
        _diagnostics = diagnostics;
    }

    /// <summary>Items whose shape mesher is not written yet.</summary>
    public int GeometryPending { get; private set; }

    public Model3D Build(IReadOnlyList<StepPlacedItem> placements)
    {
        foreach (var p in placements)
            if (MeshOf(p) is { } mesh)
                _builder.AddInstance(mesh, p.Transform,
                    p.Colour is { } c ? Material.Default.WithColor(c) : _options.UnstyledMaterial,
                    p.ProductDefinitionId != 0 ? p.ProductDefinitionId : p.ItemId);
        if (GeometryPending > 0)
            _diagnostics.Record(nameof(StepShapeMesher), StepSupport.Unsupported, $"geometry pending for {GeometryPending} shape items");
        return _builder.Build();
    }

    int? MeshOf(StepPlacedItem p)
    {
        if (_meshOfItem.TryGetValue(p.ItemId, out var cached))
            return cached;
        var item = _model.Get(p.ItemId);
        int? index = null;
        try
        {
            if (_model.MeshItem(item, p.Units, _diagnostics, _options.MaxDeviation) is { Mesh.FaceIndices.Count: > 0 } shape)
                index = _builder.AddMeshWithoutInstance(shape.Mesh);
        }
        catch (NotImplementedException)
        {
            GeometryPending++;
        }
        catch (Exception ex)
        {
            _diagnostics.Record(item.EntityName(), StepSupport.Unsupported, $"#{item.Id}: {ex.GetType().Name}: {ex.Message}");
        }
        return _meshOfItem[p.ItemId] = index;
    }
}
