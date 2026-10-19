using Ara3D.Models;
using Ara3D.Step.Entities;
using Ara3D.Step.Mesher.Plato;

namespace Ara3D.Step.Mesher;

/// <summary>Takes each placed item's mesh once from the file's meshes, through <see cref="StepShapeMesher.MeshItem"/>, and
/// adds one instance per placement, sharing the item's mesh. An item every face of which is pending in the library
/// counts as geometry pending, and so does every item when the file could not be meshed at all (a null file, recorded
/// by the caller); any other exception is recorded against the item.</summary>
public sealed class StepModelBuilder
{
    readonly StepModel _model;
    readonly StepPlatoMesh? _file;
    readonly StepMeshingOptions _options;
    readonly StepMeshingDiagnostics _diagnostics;
    readonly Dictionary<int, int?> _meshOfItem = new();
    readonly Model3DBuilder _builder = new();

    public StepModelBuilder(StepModel model, StepPlatoMesh? file, StepMeshingOptions options, StepMeshingDiagnostics diagnostics)
    {
        _model = model;
        _file = file;
        _options = options;
        _diagnostics = diagnostics;
    }

    /// <summary>Items whose every face waits on library code not written yet.</summary>
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
            var shape = _file is { } file ? file.MeshItem(_model, item, p.Units, _diagnostics) : null;
            if (_file is null || shape is { IsPending: true })
                GeometryPending++;
            else if (shape is { Mesh.FaceIndices.Count: > 0 })
                index = _builder.AddMeshWithoutInstance(shape.Mesh);
        }
        catch (Exception ex)
        {
            _diagnostics.Record(item.EntityName(), StepSupport.Unsupported, $"#{item.Id}: {ex.GetType().Name}: {ex.Message}");
        }
        return _meshOfItem[p.ItemId] = index;
    }
}
