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

    /// <summary>The representations a shape_representation_relationship without a transformation ties to this one:
    /// the same shape in the same space, typically a part's shape_representation and its B-rep representation.</summary>
    public IReadOnlyList<int> SameShape(int representationId)
        => _sameShape.TryGetValue(representationId, out var ids) ? ids : [];

    void Add(StepEntity e)
    {
        if (e.Is<IShapeRepresentation>())
            _shapeRepresentations.Add(e.Id);
        else if (e.Is<IRepresentationMap>())
            AddId(_mapped, e.Ref(RepresentationMap.Instance.MappedRepresentation));
        else if (e.Is<IShapeRepresentationRelationship>() && !e.Is<IRepresentationRelationshipWithTransformation>())
            AddSameShape(e);
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
