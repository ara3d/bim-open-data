using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

public sealed partial class OccurrenceFinder
{
    /// <summary>The map from the component representation's millimetre coordinates to the assembly
    /// representation's. Which side is the component is decided by membership, never by attribute position:
    /// exporters swap rep_1 and rep_2, and the two items of an item_defined_transformation. Null when the
    /// transformation is neither an item_defined_transformation nor a cartesian_transformation_operator_3d, or a
    /// frame is singular.</summary>
    Affine? ComponentMap(StepEntity relationship, IReadOnlySet<int> componentShape)
    {
        var rep1 = relationship.Ref(ST.RepresentationRelationship.Instance.Rep1)!.Value;
        var rep2 = relationship.Ref(ST.RepresentationRelationship.Instance.Rep2)!.Value;
        var componentIsRep1 = componentShape.Contains(rep1.Id) || !componentShape.Contains(rep2.Id);
        var (componentRep, assemblyRep) = componentIsRep1 ? (rep1, rep2) : (rep2, rep1);
        if (relationship.Ref(ST.RepresentationRelationshipWithTransformation.Instance.TransformationOperator) is not { } op)
            return null;
        var assemblyFactors = FactorsOf(assemblyRep);
        if (op.Is<ST.IItemDefinedTransformation>())
            return ItemDefined(op, componentIsRep1, componentRep, assemblyRep, assemblyFactors);
        var frame = op.PlacementMap(assemblyFactors.MillimetresPerUnit, _findings);
        return componentIsRep1 ? frame : frame?.Inverse();
    }

    Affine? ItemDefined(StepEntity op, bool componentIsRep1, StepEntity componentRep, StepEntity assemblyRep, ContextFactors assemblyFactors)
    {
        var item1 = op.Ref(ST.ItemDefinedTransformation.Instance.TransformItem1)!.Value;
        var item2 = op.Ref(ST.ItemDefinedTransformation.Instance.TransformItem2)!.Value;
        var (componentItem, assemblyItem) = componentIsRep1 ? (item1, item2) : (item2, item1);
        var componentItems = (componentRep.Refs(ST.Representation.Instance.Items) ?? []).Select(i => i.Id).ToHashSet();
        if (!componentItems.Contains(componentItem.Id) && componentItems.Contains(assemblyItem.Id))
        {
            (componentItem, assemblyItem) = (assemblyItem, componentItem);
            _findings.Once(StepDiagnosticCode.PlacementItemsSwapped(), op.Id);
        }
        var from = componentItem.PlacementMap(FactorsOf(componentRep).MillimetresPerUnit, _findings)?.Inverse();
        var to = assemblyItem.PlacementMap(assemblyFactors.MillimetresPerUnit, _findings);
        return from is { } f && to is { } t ? f.Then(t) : null;
    }

    ContextFactors FactorsOf(StepEntity representation)
        => _factors(representation.Ref(ST.Representation.Instance.ContextOfItems)?.Id ?? -1);
}
