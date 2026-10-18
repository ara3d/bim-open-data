using Ara3D.Step.Entities;
using Ara3D.StepTypes;

namespace Ara3D.Step.Mesher;

/// <summary>
/// The inverse links the mesher follows, found in one pass over the file: STEP stores each link on one side only
/// (a shape_representation_relationship names its two representations; nothing on a representation names the
/// relationship). Keys and values are instance ids.
/// </summary>
public sealed class StepModelIndex
{
    readonly Dictionary<int, List<int>> _sameShape = new();
    readonly HashSet<int> _mapped = new();
    readonly List<int> _shapeRepresentations = new();
    readonly List<int> _productDefinitions = new();
    readonly HashSet<int> _components = new();
    readonly Dictionary<int, List<int>> _usages = new();
    readonly Dictionary<int, List<int>> _shapesOfDefinition = new();
    readonly Dictionary<int, List<int>> _representationsOfShape = new();
    readonly Dictionary<int, int> _placementOfShape = new();

    public StepModelIndex(StepModel model, StepMeshingDiagnostics diagnostics)
    {
        for (var i = 0; i < model.Count; i++)
        {
            var e = model.EntityAt(i);
            diagnostics.Guard(e, () => Add(e));
        }
    }

    /// <summary>Every shape_representation and subtype, in file order.</summary>
    public IReadOnlyList<int> ShapeRepresentations => _shapeRepresentations;

    /// <summary>Representations some representation_map places, so they are reached through a mapped_item.</summary>
    public IReadOnlySet<int> MappedRepresentations => _mapped;

    /// <summary>The representation and every one tied to it, directly or in a chain, by a
    /// shape_representation_relationship without a transformation: the same shape in the same space, typically a
    /// part's shape_representation and its B-rep representation.</summary>
    public IReadOnlyList<int> SameShape(int representationId)
    {
        var found = new List<int> { representationId };
        var seen = new HashSet<int> { representationId };
        for (var i = 0; i < found.Count; i++)
            if (_sameShape.TryGetValue(found[i], out var linked))
                foreach (var id in linked)
                    if (seen.Add(id))
                        found.Add(id);
        return found;
    }

    /// <summary>Every product_definition, in file order.</summary>
    public IReadOnlyList<int> ProductDefinitions => _productDefinitions;

    /// <summary>True when a next_assembly_usage_occurrence names the product_definition as its component.</summary>
    public bool IsComponent(int productDefinitionId)
        => _components.Contains(productDefinitionId);

    /// <summary>The next_assembly_usage_occurrences whose assembly is this product_definition.</summary>
    public IReadOnlyList<int> Usages(int productDefinitionId)
        => _usages.TryGetValue(productDefinitionId, out var ids) ? ids : [];

    /// <summary>The representations a product_definition or an occurrence has as its shape, through
    /// product_definition_shape and shape_definition_representation.</summary>
    public IReadOnlyList<int> Representations(int definitionId)
        => _shapesOfDefinition.TryGetValue(definitionId, out var shapes)
            ? shapes.SelectMany(s => _representationsOfShape.TryGetValue(s, out var reps) ? reps : []).ToList()
            : [];

    /// <summary>The representation relationship with a transformation that places an occurrence, through its
    /// product_definition_shape and context_dependent_shape_representation; null when none does.</summary>
    public int? Placement(int occurrenceId)
    {
        if (_shapesOfDefinition.TryGetValue(occurrenceId, out var shapes))
            foreach (var s in shapes)
                if (_placementOfShape.TryGetValue(s, out var relationship))
                    return relationship;
        return null;
    }

    void Add(StepEntity e)
    {
        if (e.Is<IShapeRepresentation>())
            _shapeRepresentations.Add(e.Id);
        else if (e.Is<IProductDefinition>())
            _productDefinitions.Add(e.Id);
        else if (e.Is<INextAssemblyUsageOccurrence>())
            AddUsage(e);
        else if (e.Is<IProductDefinitionShape>() && e.Ref(ProductDefinitionShape.Instance.Definition) is { } definition)
            Add(_shapesOfDefinition, definition.Id, e.Id);
        else if (e.Is<IShapeDefinitionRepresentation>())
            AddShapeDefinition(e);
        else if (e.Is<IContextDependentShapeRepresentation>())
            AddContextDependentShape(e);
        else if (e.Is<IRepresentationMap>())
            AddId(_mapped, e.Ref(RepresentationMap.Instance.MappedRepresentation));
        else if (e.Is<IShapeRepresentationRelationship>() && !e.Is<IRepresentationRelationshipWithTransformation>())
            AddSameShape(e);
    }

    void AddUsage(StepEntity usage)
    {
        var assembly = usage.Ref(NextAssemblyUsageOccurrence.Instance.RelatingProductDefinition);
        var component = usage.Ref(NextAssemblyUsageOccurrence.Instance.RelatedProductDefinition);
        if (assembly is not { } a || component is not { } c)
            return;
        Add(_usages, a.Id, usage.Id);
        _components.Add(c.Id);
    }

    void AddShapeDefinition(StepEntity e)
    {
        var shape = e.Ref(ShapeDefinitionRepresentation.Instance.Definition);
        var rep = e.Ref(ShapeDefinitionRepresentation.Instance.UsedRepresentation);
        if (shape is { } s && rep is { } r)
            Add(_representationsOfShape, s.Id, r.Id);
    }

    void AddContextDependentShape(StepEntity e)
    {
        var relationship = e.Ref(ContextDependentShapeRepresentation.Instance.RepresentationRelation);
        var shape = e.Ref(ContextDependentShapeRepresentation.Instance.RepresentedProductRelation);
        if (relationship is { } r && shape is { } s)
            _placementOfShape.TryAdd(s.Id, r.Id);
    }

    void AddSameShape(StepEntity relationship)
    {
        var a = relationship.Ref(RepresentationRelationship.Instance.Rep1);
        var b = relationship.Ref(RepresentationRelationship.Instance.Rep2);
        if (a is not { } rep1 || b is not { } rep2)
            return;
        Add(_sameShape, rep1.Id, rep2.Id);
        Add(_sameShape, rep2.Id, rep1.Id);
    }

    internal static void Add(Dictionary<int, List<int>> map, int key, int value)
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        list.Add(value);
    }

    static void AddId(HashSet<int> set, StepEntity? e)
    {
        if (e is { } entity)
            set.Add(entity.Id);
    }
}
