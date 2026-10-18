using Ara3D.Step.Entities;
using ST = Ara3D.StepTypes;

namespace Ara3D.Step.Decode;

/// <summary>
/// The links the occurrence walk follows backwards, found in one pass over the file: STEP stores each link on one
/// side only (a shape_representation_relationship names its representations; nothing on a representation names
/// the relationship). Keys and values are instance ids; lists keep file order. The same index lives in
/// Ara3D.Step.Mesher's StepModelIndex (TKT-312 build log: it goes when the mesher reads the decoder's occurrences).
/// </summary>
public sealed class StepLinks
{
    readonly Dictionary<int, List<int>> _sameShape = new();
    readonly Dictionary<int, List<int>> _usages = new();
    readonly Dictionary<int, List<int>> _shapesOfDefinition = new();
    readonly Dictionary<int, List<int>> _representationsOfShape = new();
    readonly Dictionary<int, int> _placementOfShape = new();
    readonly HashSet<int> _components = new();
    readonly HashSet<int> _mapped = new();
    readonly List<int> _productDefinitions = new();
    readonly List<int> _representations = new();
    readonly List<int> _contexts3D = new();

    public StepLinks(StepModel model, Action<StepEntity, Exception> malformed)
    {
        for (var i = 0; i < model.Count; i++)
        {
            var e = model.EntityAt(i);
            try
            {
                Add(e);
            }
            catch (Exception ex) when (ex is InvalidDataException or KeyNotFoundException)
            {
                malformed(e, ex);
            }
        }
        _productDefinitions.Sort();
        _representations.Sort();
        _contexts3D.Sort();
    }

    /// <summary>Every product_definition, ascending by id.</summary>
    public IReadOnlyList<int> ProductDefinitions => _productDefinitions;

    /// <summary>Every representation (shape_representation and its subtypes included), ascending by id.</summary>
    public IReadOnlyList<int> Representations => _representations;

    /// <summary>Every geometric_representation_context of dimension 3, ascending by id.</summary>
    public IReadOnlyList<int> Contexts3D => _contexts3D;

    /// <summary>Representations a representation_map places, so a mapped_item reaches them.</summary>
    public IReadOnlySet<int> MappedRepresentations => _mapped;

    public bool IsComponent(int productDefinitionId)
        => _components.Contains(productDefinitionId);

    /// <summary>The next_assembly_usage_occurrences whose assembly is this product_definition, ascending by id.</summary>
    public IReadOnlyList<int> Usages(int productDefinitionId)
        => _usages.TryGetValue(productDefinitionId, out var ids) ? ids : [];

    /// <summary>The representations a product_definition or an occurrence has as its shape, through
    /// product_definition_shape and shape_definition_representation.</summary>
    public IReadOnlyList<int> RepresentationsOf(int definitionId)
        => _shapesOfDefinition.TryGetValue(definitionId, out var shapes)
            ? shapes.SelectMany(s => _representationsOfShape.TryGetValue(s, out var reps) ? reps : []).ToList()
            : [];

    /// <summary>The context_dependent_shape_representation that places an occurrence; null when none does.</summary>
    public int? PlacementOf(int occurrenceId)
    {
        if (_shapesOfDefinition.TryGetValue(occurrenceId, out var shapes))
            foreach (var s in shapes)
                if (_placementOfShape.TryGetValue(s, out var placement))
                    return placement;
        return null;
    }

    /// <summary>The representation and every one tied to it, directly or in a chain, by a
    /// shape_representation_relationship without a transformation: the same shape in the same space.</summary>
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

    void Add(StepEntity e)
    {
        if (e.Is<ST.IRepresentation>())
            _representations.Add(e.Id);
        else if (e.Is<ST.IGeometricRepresentationContext>())
            AddContext(e);
        else if (e.Is<ST.IProductDefinition>())
            _productDefinitions.Add(e.Id);
        else if (e.Is<ST.INextAssemblyUsageOccurrence>())
            AddUsage(e);
        else if (e.Is<ST.IProductDefinitionShape>() && e.Ref(ST.ProductDefinitionShape.Instance.Definition) is { } definition)
            Append(_shapesOfDefinition, definition.Id, e.Id);
        else if (e.Is<ST.IShapeDefinitionRepresentation>())
            AddShapeDefinition(e);
        else if (e.Is<ST.IContextDependentShapeRepresentation>())
            AddContextDependentShape(e);
        else if (e.Is<ST.IRepresentationMap>() && e.Ref(ST.RepresentationMap.Instance.MappedRepresentation) is { } mapped)
            _mapped.Add(mapped.Id);
        else if (e.Is<ST.IShapeRepresentationRelationship>() && !e.Is<ST.IRepresentationRelationshipWithTransformation>())
            AddSameShape(e);
    }

    void AddContext(StepEntity e)
    {
        if (e.Integer(ST.GeometricRepresentationContext.Instance.CoordinateSpaceDimension) == 3)
            _contexts3D.Add(e.Id);
    }

    void AddUsage(StepEntity usage)
    {
        var assembly = usage.Ref(ST.NextAssemblyUsageOccurrence.Instance.RelatingProductDefinition);
        var component = usage.Ref(ST.NextAssemblyUsageOccurrence.Instance.RelatedProductDefinition);
        if (assembly is not { } a || component is not { } c)
            return;
        Append(_usages, a.Id, usage.Id);
        _components.Add(c.Id);
    }

    void AddShapeDefinition(StepEntity e)
    {
        var shape = e.Ref(ST.ShapeDefinitionRepresentation.Instance.Definition);
        var rep = e.Ref(ST.ShapeDefinitionRepresentation.Instance.UsedRepresentation);
        if (shape is { } s && rep is { } r)
            Append(_representationsOfShape, s.Id, r.Id);
    }

    void AddContextDependentShape(StepEntity e)
    {
        if (e.Ref(ST.ContextDependentShapeRepresentation.Instance.RepresentedProductRelation) is { } shape)
            _placementOfShape.TryAdd(shape.Id, e.Id);
    }

    void AddSameShape(StepEntity relationship)
    {
        var a = relationship.Ref(ST.RepresentationRelationship.Instance.Rep1);
        var b = relationship.Ref(ST.RepresentationRelationship.Instance.Rep2);
        if (a is not { } rep1 || b is not { } rep2)
            return;
        Append(_sameShape, rep1.Id, rep2.Id);
        Append(_sameShape, rep2.Id, rep1.Id);
    }

    static void Append(Dictionary<int, List<int>> map, int key, int value)
    {
        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];
        list.Add(value);
    }
}
