using Ara3D.Geometry;
using Ara3D.Step.Entities;
using Ara3D.StepTypes;
using SN = System.Numerics;

namespace Ara3D.Step.Mesher;

/// <summary>
/// Walks representations and lists every shape item with its accumulated transform, without meshing anything.
/// A representation brings in the representations tied to it by a shape_representation_relationship without a
/// transformation, and each mapped_item places its representation_map's representation so that mapping_origin
/// lands on mapping_target. Placement items are frames for those transforms and are not shapes. The assembly
/// half, StepShapeCollector.Assembly.cs, walks products.
/// </summary>
public sealed partial class StepShapeCollector
{
    readonly StepModel _model;
    readonly StepModelIndex _index;
    readonly StepMeshingDiagnostics _diagnostics;
    readonly List<StepPlacedItem> _placed = [];
    readonly List<StepCycle> _cycles = [];
    readonly HashSet<int> _reached = [];
    readonly List<int> _mapPath = [];

    public StepShapeCollector(StepModel model, StepMeshingDiagnostics diagnostics)
    {
        _model = model;
        _diagnostics = diagnostics;
        _index = new StepModelIndex(model, diagnostics);
        Units = new StepContextUnits(diagnostics);
        Styles = new StepStyles(model, _index.StyledItems, diagnostics);
    }

    public StepContextUnits Units { get; }
    public StepStyles Styles { get; }
    public IReadOnlyList<StepPlacedItem> Placed => _placed;
    public IReadOnlyList<StepCycle> Cycles => _cycles;

    /// <summary>Places the items of a representation and of every representation of the same shape.</summary>
    public void AddShape(StepEntity representation, SN.Matrix4x4 transform, int depth, int productDefinitionId, Color? colour = null)
    {
        foreach (var id in _index.SameShape(representation.Id))
        {
            _reached.Add(id);
            var rep = _model.Get(id);
            _diagnostics.Guard(rep, () => AddItems(rep, transform, depth, productDefinitionId, Styles.Of(id) ?? colour));
        }
    }

    /// <summary>Places, at the origin, every shape representation that no product or mapped_item reached.</summary>
    public void AddUnreachedShapes()
    {
        foreach (var id in _index.ShapeRepresentations)
        {
            if (_reached.Contains(id) || _index.MappedRepresentations.Contains(id))
                continue;
            var rep = _model.Get(id);
            _diagnostics.Record(rep.EntityName(), StepSupport.Supported, $"#{id} is reached from no product; placed at the origin");
            AddShape(rep, SN.Matrix4x4.Identity, 0, 0);
        }
    }

    void AddItems(StepEntity rep, SN.Matrix4x4 transform, int depth, int productDefinitionId, Color? colour)
    {
        var repeat = _mapPath.IndexOf(rep.Id);
        if (repeat >= 0)
        {
            AddCycle(MappedItem.Instance.EntityName, _mapPath, repeat, rep.Id);
            return;
        }
        _mapPath.Add(rep.Id);
        var units = Units.Of(rep);
        foreach (var item in rep.Refs(Representation.Instance.Items) ?? [])
            _diagnostics.Guard(item, () => AddItem(item, rep, units, transform, depth, productDefinitionId, colour));
        _mapPath.RemoveAt(_mapPath.Count - 1);
    }

    void AddItem(StepEntity item, StepEntity rep, StepUnits units, SN.Matrix4x4 transform, int depth, int productDefinitionId, Color? colour)
    {
        if (item.Is<IMappedItem>())
            AddMapped(item, units, transform, depth, productDefinitionId, Styles.Of(item.Id) ?? colour);
        else if (IsShape(item))
            _placed.Add(new(item.Id, rep.Id, productDefinitionId, transform, depth, units, Styles.OfShape(item) ?? colour));
        else if (!item.Is<IPlacement>())
            _diagnostics.Record(item.EntityName(), StepSupport.Unsupported);
    }

    void AddMapped(StepEntity item, StepUnits units, SN.Matrix4x4 transform, int depth, int productDefinitionId, Color? colour)
    {
        var map = item.Ref(MappedItem.Instance.MappingSource);
        var target = item.Ref(MappedItem.Instance.MappingTarget);
        var mapped = map?.Ref(RepresentationMap.Instance.MappedRepresentation);
        var origin = map?.Ref(RepresentationMap.Instance.MappingOrigin);
        if (target is not { } t || mapped is not { } rep || origin is not { } o)
        {
            _diagnostics.Record(item.EntityName(), StepSupport.Unsupported, $"#{item.Id} lacks its source, origin or target");
            return;
        }
        var mapping = StepPlacements.Between(o, Units.Of(rep), t, units);
        _diagnostics.Record(item.EntityName(), mapping is null ? StepSupport.Approximate : StepSupport.Supported,
            mapping is null ? $"#{item.Id}: origin or target is not a placement; placed without a transform" : null);
        AddShape(rep, (mapping ?? SN.Matrix4x4.Identity) * transform, depth, productDefinitionId, colour);
    }

    void AddCycle(string link, List<int> path, int repeat, int id)
    {
        var cycle = new StepCycle(link, [.. path.Skip(repeat), id]);
        _cycles.Add(cycle);
        _diagnostics.Record(link, StepSupport.Unsupported, $"cycle {string.Join(" -> ", cycle.Path.Select(i => $"#{i}"))} not followed");
    }

    static bool IsShape(StepEntity item)
        => item.Is<ISolidModel>() || item.Is<IShellBasedSurfaceModel>() || item.Is<IFaceBasedSurfaceModel>()
            || item.Is<IConnectedFaceSet>() || item.Is<IFace>();
}
