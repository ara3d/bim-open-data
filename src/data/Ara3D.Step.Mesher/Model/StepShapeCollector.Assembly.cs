using Ara3D.Step.Entities;
using Ara3D.StepTypes;
using SN = System.Numerics;

namespace Ara3D.Step.Mesher;

/// <summary>
/// The assembly walk (STEP-BREP-MODEL.md section 3.8, after the PDM usage guide 4.4): from each root
/// product_definition down its next_assembly_usage_occurrences, placing each occurrence by the representation
/// relationship with transformation its context_dependent_shape_representation names, and composing transforms
/// from the root down, so a component used twice is placed twice.
/// </summary>
public sealed partial class StepShapeCollector
{
    readonly List<int> _productPath = [];
    readonly HashSet<int> _visitedProducts = [];

    /// <summary>Walks every root product_definition, then any product only a cycle reaches.</summary>
    public void AddProducts()
    {
        foreach (var id in _index.ProductDefinitions)
            if (!_index.IsComponent(id))
                AddProduct(id, SN.Matrix4x4.Identity, 0);
        foreach (var id in _index.ProductDefinitions)
            if (!_visitedProducts.Contains(id))
                AddProduct(id, SN.Matrix4x4.Identity, 0);
    }

    void AddProduct(int id, SN.Matrix4x4 transform, int depth)
    {
        var repeat = _productPath.IndexOf(id);
        if (repeat >= 0)
        {
            AddCycle(NextAssemblyUsageOccurrence.Instance.EntityName, _productPath, repeat, id);
            return;
        }
        _productPath.Add(id);
        _visitedProducts.Add(id);
        var firstPlaced = _placed.Count;
        foreach (var repId in _index.Representations(id))
            AddShape(_model.Get(repId), transform, depth, id);
        var ownPlaced = _placed.Count;
        foreach (var usageId in _index.Usages(id))
        {
            var usage = _model.Get(usageId);
            _diagnostics.Guard(usage, () => AddUsage(usage, transform, depth, firstPlaced, ownPlaced));
        }
        _productPath.RemoveAt(_productPath.Count - 1);
    }

    void AddUsage(StepEntity usage, SN.Matrix4x4 transform, int depth, int firstPlaced, int ownPlaced)
    {
        var component = usage.Ref(NextAssemblyUsageOccurrence.Instance.RelatedProductDefinition)!.Value;
        var shape = ComponentShape(component.Id);
        SN.Matrix4x4? placement;
        if (_index.Placement(usage.Id) is { } relationshipId)
            placement = Occurrence(_model.Get(relationshipId), shape);
        else if (PlacedBetween(firstPlaced, ownPlaced, shape))
        {
            _diagnostics.Record(usage.EntityName(), StepSupport.Supported, $"#{usage.Id} is placed by a mapped_item of its assembly");
            return;
        }
        else
            placement = null;
        _diagnostics.Record(usage.EntityName(), placement is null ? StepSupport.Approximate : StepSupport.Supported,
            placement is null ? $"#{usage.Id} has no usable placement; component placed at the assembly origin" : null);
        AddProduct(component.Id, (placement ?? SN.Matrix4x4.Identity) * transform, depth + 1);
    }

    /// <summary>The transform from the component's representation into the assembly's. Which side is the component
    /// is decided by membership, never by attribute position: exporters swap rep_1 and rep_2, and the two items
    /// of an item_defined_transformation.</summary>
    SN.Matrix4x4? Occurrence(StepEntity relationship, IReadOnlySet<int> componentShape)
    {
        var rep1 = relationship.Ref(RepresentationRelationship.Instance.Rep1)!.Value;
        var rep2 = relationship.Ref(RepresentationRelationship.Instance.Rep2)!.Value;
        var componentIsRep1 = componentShape.Contains(rep1.Id) || !componentShape.Contains(rep2.Id);
        var (componentRep, assemblyRep) = componentIsRep1 ? (rep1, rep2) : (rep2, rep1);
        var op = relationship.Ref(RepresentationRelationshipWithTransformation.Instance.TransformationOperator);
        if (op is not { } transformation)
            return null;
        if (transformation.Is<IItemDefinedTransformation>())
            return ItemDefined(transformation, componentIsRep1, componentRep, assemblyRep);
        if (transformation.Frame(Units.Of(assemblyRep)) is not { } frame)
        {
            _diagnostics.Record(transformation.EntityName(), StepSupport.Unsupported, $"#{transformation.Id} is not a placement transformation");
            return null;
        }
        _diagnostics.Record(transformation.EntityName(), StepSupport.Supported);
        return componentIsRep1 ? frame : SN.Matrix4x4.Invert(frame, out var inverse) ? inverse : null;
    }

    SN.Matrix4x4? ItemDefined(StepEntity transformation, bool componentIsRep1, StepEntity componentRep, StepEntity assemblyRep)
    {
        var item1 = transformation.Ref(ItemDefinedTransformation.Instance.TransformItem1)!.Value;
        var item2 = transformation.Ref(ItemDefinedTransformation.Instance.TransformItem2)!.Value;
        var (componentItem, assemblyItem) = componentIsRep1 ? (item1, item2) : (item2, item1);
        var componentItems = ItemIds(componentRep);
        if (!componentItems.Contains(componentItem.Id) && componentItems.Contains(assemblyItem.Id))
            (componentItem, assemblyItem) = (assemblyItem, componentItem);
        var between = StepPlacements.Between(componentItem, Units.Of(componentRep), assemblyItem, Units.Of(assemblyRep));
        _diagnostics.Record(transformation.EntityName(), between is null ? StepSupport.Unsupported : StepSupport.Supported,
            between is null ? $"#{transformation.Id}: an item is not a placement" : null);
        return between;
    }

    IReadOnlySet<int> ComponentShape(int productDefinitionId)
        => _index.Representations(productDefinitionId).SelectMany(_index.SameShape).ToHashSet();

    bool PlacedBetween(int first, int end, IReadOnlySet<int> shape)
    {
        for (var i = first; i < end; i++)
            if (shape.Contains(_placed[i].RepresentationId))
                return true;
        return false;
    }

    static HashSet<int> ItemIds(StepEntity representation)
        => (representation.Refs(Representation.Instance.Items) ?? []).Select(i => i.Id).ToHashSet();
}
