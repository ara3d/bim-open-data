using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>
/// Finds the occurrences as the Plato library's FindOccurrences specifies (research/STEP-BREP-MODEL.md 3.8): from
/// each root product_definition, ascending by id, down its next_assembly_usage_occurrences, each placed by the
/// representation relationship its context_dependent_shape_representation names, with mapped_items composed
/// origin onto target to any depth and cycles cut. A representation is an occurrence when it holds a
/// manifold_solid_brep (or subtype) or a shell_based_surface_model; representations no product reaches become roots
/// at the identity unless they repeat an item already placed. Roots come first. The rules follow
/// Ara3D.Step.Mesher's StepShapeCollector, in double precision and millimetres.
/// </summary>
public sealed partial class OccurrenceFinder
{
    readonly StepModel _model;
    readonly StepLinks _links;
    readonly Func<int, ContextFactors> _factors;
    readonly DecodeFindings _findings;
    readonly List<FoundOccurrence> _found = [];
    readonly HashSet<int> _reachedRepresentations = [];
    readonly HashSet<int> _visitedProducts = [];
    readonly HashSet<int> _placedItems = [];
    readonly List<int> _productPath = [];
    readonly List<int> _mapPath = [];

    public OccurrenceFinder(StepModel model, StepLinks links, Func<int, ContextFactors> factors, DecodeFindings findings)
    {
        _model = model;
        _links = links;
        _factors = factors;
        _findings = findings;
    }

    public IReadOnlyList<FoundOccurrence> Find()
    {
        foreach (var id in _links.ProductDefinitions)
            if (!_links.IsComponent(id))
                WalkProduct(id, Affine.Identity, -1, -1);
        foreach (var id in _links.ProductDefinitions)
            if (!_visitedProducts.Contains(id))
                WalkProduct(id, Affine.Identity, -1, -1);
        foreach (var id in _links.Representations)
            if (!_reachedRepresentations.Contains(id) && !_links.MappedRepresentations.Contains(id) && !RepeatsPlacedItem(id))
                Guarded(id, () => VisitRepresentation(id, Affine.Identity, -1, -1));
        return RootsFirst(_found);
    }

    /// <summary>The B-rep items of a representation: manifold_solid_brep and its subtypes, shell_based_surface_model.</summary>
    public static IReadOnlyList<StepEntity> BrepItems(StepEntity representation)
        => (representation.Refs(ST.Representation.Instance.Items) ?? []).Where(IsBrepItem).ToList();

    public static bool IsBrepItem(StepEntity item)
        => item.Is<ST.IManifoldSolidBrep>() || item.Is<ST.IShellBasedSurfaceModel>();

    void WalkProduct(int id, Affine space, int parent, int via)
    {
        var repeat = _productPath.IndexOf(id);
        if (repeat >= 0)
        {
            _findings.Once(StepDiagnosticCode.AssemblyCycle(), id);
            return;
        }
        _productPath.Add(id);
        _visitedProducts.Add(id);
        var own = parent;
        var ownRepresentations = new HashSet<int>();
        foreach (var rep in _links.RepresentationsOf(id))
            foreach (var same in _links.SameShape(rep))
                if (ownRepresentations.Add(same))
                    Guarded(same, () =>
                    {
                        var index = VisitRepresentation(same, space, parent, via, ownRepresentations);
                        if (own == parent && index >= 0)
                            own = index;
                    });
        foreach (var usage in _links.Usages(id).Order())
            Guarded(usage, () => WalkUsage(_model.Get(usage), space, own, ownRepresentations));
        _productPath.RemoveAt(_productPath.Count - 1);
    }

    void WalkUsage(StepEntity usage, Affine assemblySpace, int parent, IReadOnlySet<int> assemblyRepresentations)
    {
        var component = usage.Ref(ST.NextAssemblyUsageOccurrence.Instance.RelatedProductDefinition)!.Value.Id;
        var shape = _links.RepresentationsOf(component).SelectMany(_links.SameShape).ToHashSet();
        if (_links.PlacementOf(usage.Id) is not { } placing)
        {
            if (!shape.Overlaps(assemblyRepresentations))
                WalkProduct(component, assemblySpace, parent, usage.Id);
            return;
        }
        var relationship = _model.Get(placing).Ref(ST.ContextDependentShapeRepresentation.Instance.RepresentationRelation)!.Value;
        var map = ComponentMap(relationship, shape) ?? Affine.Identity;
        WalkProduct(component, map.Then(assemblySpace), parent, placing);
    }

    /// <summary>Visits a representation placed by <paramref name="space"/> (its millimetre coordinates to the
    /// model's); returns the occurrence it became, or -1.</summary>
    int VisitRepresentation(int id, Affine space, int parent, int via, HashSet<int>? reached = null)
    {
        if (_mapPath.Contains(id))
        {
            _findings.Once(StepDiagnosticCode.AssemblyCycle(), id);
            return -1;
        }
        _reachedRepresentations.Add(id);
        reached?.Add(id);
        var rep = _model.Get(id);
        var context = rep.Ref(ST.Representation.Instance.ContextOfItems)?.Id ?? -1;
        var factors = _factors(context);
        var index = -1;
        var items = BrepItems(rep);
        if (items.Count > 0)
        {
            index = _found.Count;
            _found.Add(new(id, context, parent, via, Affine.Scale(factors.MillimetresPerUnit).Then(space)));
            foreach (var item in items)
                _placedItems.Add(item.Id);
        }
        _mapPath.Add(id);
        foreach (var item in rep.Refs(ST.Representation.Instance.Items) ?? [])
            if (item.Is<ST.IMappedItem>())
                Guarded(item.Id, () => VisitMapped(item, factors, space, index >= 0 ? index : parent, reached));
        _mapPath.RemoveAt(_mapPath.Count - 1);
        return index;
    }

    void VisitMapped(StepEntity item, ContextFactors factors, Affine space, int parent, HashSet<int>? reached)
    {
        var map = item.Ref(ST.MappedItem.Instance.MappingSource)!.Value;
        var mapped = map.Ref(ST.RepresentationMap.Instance.MappedRepresentation)!.Value;
        var origin = map.Ref(ST.RepresentationMap.Instance.MappingOrigin)!.Value;
        var target = item.Ref(ST.MappedItem.Instance.MappingTarget)!.Value;
        var mappedFactors = _factors(mapped.Ref(ST.Representation.Instance.ContextOfItems)?.Id ?? -1);
        if (origin.PlacementMap(mappedFactors.MillimetresPerUnit, _findings)?.Inverse() is not { } fromOrigin
            || target.PlacementMap(factors.MillimetresPerUnit, _findings) is not { } toTarget)
        {
            _findings.Once(StepDiagnosticCode.InstanceSkipped(), item.Id);
            return;
        }
        var mappedSpace = fromOrigin.Then(toTarget).Then(space);
        foreach (var same in _links.SameShape(mapped.Id))
            Guarded(same, () => VisitRepresentation(same, mappedSpace, parent, item.Id, reached));
    }

    bool RepeatsPlacedItem(int representationId)
    {
        var rep = _model.Get(representationId);
        return Enumerable.Any(BrepItems(rep), item => _placedItems.Contains(item.Id));
    }

    void Guarded(int id, Action visit)
    {
        try
        {
            visit();
        }
        catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException or InvalidOperationException or DecodeException)
        {
            _findings.Once(ex is DecodeException d ? d.Code : StepDiagnosticCode.InstanceSkipped(), ex is DecodeException e ? e.InstanceId : id);
        }
    }

    static IReadOnlyList<FoundOccurrence> RootsFirst(List<FoundOccurrence> found)
    {
        var order = Enumerable.Range(0, found.Count).Where(i => found[i].Parent < 0)
            .Concat(Enumerable.Range(0, found.Count).Where(i => found[i].Parent >= 0)).ToList();
        var position = new int[found.Count];
        for (var k = 0; k < order.Count; k++)
            position[order[k]] = k;
        return order.Select(i => found[i] with { Parent = found[i].Parent < 0 ? -1 : position[found[i].Parent] }).ToList();
    }
}
