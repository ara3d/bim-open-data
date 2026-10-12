using System.Runtime.CompilerServices;
using Ara3D.BimOpenSchema;
using Xbim.InformationSpecifications;
using PartOfRelation = Xbim.InformationSpecifications.PartOfFacet.PartOfRelation;

namespace Ara3D.Ids;

/// <summary>The material and part-of facets, read from the model's BOS relations.</summary>
internal static class IdsRelationFacets
{
    /// <summary>Why the facet cannot be evaluated over this model at all, or null when it can.</summary>
    public static string? Unsupported(IFacet facet, IdsModel model)
    {
        if (model.Relations is not { } relations)
            return $"{IdsFacets.FacetName(facet)} facets need the relations (the RelationText table), which were not given";
        if (facet is MaterialFacet)
            return relations.Carries(RelationType.HasMaterial) || relations.Carries(RelationType.HasLayer)
                ? null
                : "the model has no HasMaterial or HasLayer relation, so its materials cannot be told";
        if (facet is not PartOfFacet partOf)
            return null;
        if (Walk(partOf) is not { } walk)
            return $"part-of relation {Stated(partOf)} is not carried in BOS";
        var missing = walk.Needs.Where(n => !relations.Carries(n)).ToList();
        return missing.Count == 0
            ? null
            : $"the model has no {string.Join(" or ", missing)} relation, so part-of through {walk.Name} cannot be told";
    }

    private static string Stated(PartOfFacet facet)
        => UnknownRelations.TryGetValue(facet, out var stated) ? stated : facet.EntityRelation;

    public static bool IsSupported(IFacet facet)
        => facet is MaterialFacet || facet is PartOfFacet f && Walk(f) is not null;

    // ---- material ----

    /// <summary>IDS 1.0 matches a material facet's value against the name or category of any
    /// material, layer, constituent, profile, or set the entity is associated with. The converter
    /// (Ara3D.IfcLoader's IfcMaterialSelectResolver) writes an element HasMaterial its material,
    /// list members or constituents' materials, and HasLayer each layer of its layer set, each
    /// layer HasMaterial its material; a layer set reached through HasMaterial and HasLayer is
    /// followed too. The names read are each reached entity's Name, its <c>Category</c>
    /// attribute, and a layer's <c>LayerSet</c> (the converter writes the set's name there).
    /// <para>IfcRelAssociatesMaterial may relate only the type object, and the converter then
    /// writes HasMaterial on the type entity: an occurrence with no material of its own reads its
    /// type's, and one whose type cannot be told gives InfoNotAvailable.</para></summary>
    public static FacetMatch MatchMaterial(MaterialFacet facet, IdsEntity entity, IdsModel model, IdsWarnings warnings)
    {
        // A layer or layer set has edges to its materials too, but IDS associates materials with
        // objects; without this a material applicability would select the layers themselves.
        if (IsMaterialDefinition(entity))
            return FacetMatch.Absent($"{entity.Category} is a material definition, not an object with a material");
        var names = entity.WithInherited(e => MaterialNames(e, model), _ => true);
        if (names.Count == 0 && IdsFacets.UnresolvedType(entity, "no material of its own", warnings) is { } unknown)
            return unknown;
        if (names.Count == 0)
            return FacetMatch.Absent("no material");
        if (IdsFacets.IsEmpty(facet.Value) || names.Any(n => IdsFacets.Satisfies(facet.Value, n, ignoreCase: false)))
            return FacetMatch.Yes;
        return FacetMatch.No($"materials {string.Join(", ", names.Select(n => $"'{n}'"))} are not {facet.Value!.Short()}");
    }

    private static bool IsMaterialDefinition(IdsEntity entity)
        => entity.Category.StartsWith("IFCMATERIAL", StringComparison.OrdinalIgnoreCase);

    private static readonly RelationType[] MaterialEdges = [RelationType.HasMaterial, RelationType.HasLayer];

    /// <summary>Only IFCMATERIAL* entities are read: a reader of inverse attributes (That Open's
    /// Fragments) writes every IfcRelAssociates as HasMaterial, classification references too
    /// (see Ara3D.Ifc.Conventions' IfcRelationNames).</summary>
    private static List<string> MaterialNames(IdsEntity entity, IdsModel model)
    {
        var names = new List<string>();
        foreach (var reached in Reach(entity.EntityIndex, model, MaterialEdges, IsMaterialDefinition))
        {
            names.Add(reached.Name);
            names.Add(reached.Attribute("Category")?.Text ?? "");
            names.Add(reached.Attribute("LayerSet")?.Text ?? "");
        }
        return names.Where(n => n.Length > 0).Distinct().ToList();
    }

    // ---- part-of ----

    /// <summary>How one IDS 1.0 part-of relation is followed upward in BOS. <see cref="Needs"/>
    /// lists the BOS types it reads; a model without one of them cannot tell a Fail.</summary>
    private sealed record PartOfWalk(string Name, RelationType[] Needs, Func<long, IdsModel, IEnumerable<IdsEntity>> Containers);

    private const string AnyRelation = "any relation";

    /// <summary>The IDS 1.0 part-of relations as BOS relation types. This mapping mirrors the IFC
    /// converter, Ara3D.IfcLoader's IfcRelations.ParseEntity (and, for readers of inverse
    /// attributes, Ara3D.Ifc.Conventions' IfcRelationNames.Relations): IfcRelAggregates and
    /// IfcRelAssignsToGroup become MemberOf, told apart here by whether the target is a group;
    /// IfcRelContainedInSpatialStructure becomes ContainedIn; IfcRelNests becomes ChildOf;
    /// IfcRelFillsElement becomes Fills (element to opening) and IfcRelVoidsElement Voids (opening
    /// to host). Change the two together.
    /// <para>IDS 1.0 says an entity is part of a container through the relation, recursively:
    /// a wall aggregated into an assembly aggregated into another is part of both. For spatial
    /// containment the walk continues up the spatial structure, which IFC builds with
    /// IfcRelAggregates, so a door contained in a storey is part of the storey's building and
    /// site; a container's own containment is not followed, since IFC does not contain a spatial
    /// element in another. IFCRELVOIDSELEMENT IFCRELFILLSELEMENT is one IDS value: the element
    /// fills an opening that voids the container. With no relation, any of these.</para></summary>
    private static readonly Dictionary<PartOfRelation, PartOfWalk> Walks = new()
    {
        [PartOfRelation.IfcRelAggregates] = new("IFCRELAGGREGATES", [RelationType.MemberOf],
            (e, m) => Up(e, m, RelationType.MemberOf, c => !IsGroup(c))),
        [PartOfRelation.IfcRelAssignsToGroup] = new("IFCRELASSIGNSTOGROUP", [RelationType.MemberOf],
            (e, m) => Up(e, m, RelationType.MemberOf, IsGroup)),
        [PartOfRelation.IfcRelContainedInSpatialStructure] = new("IFCRELCONTAINEDINSPATIALSTRUCTURE", [RelationType.ContainedIn, RelationType.MemberOf],
            (e, m) => Step(e, m, RelationType.ContainedIn).SelectMany(s => Up(s.EntityIndex, m, RelationType.MemberOf, c => !IsGroup(c)).Prepend(s))),
        [PartOfRelation.IfcRelNests] = new("IFCRELNESTS", [RelationType.ChildOf],
            (e, m) => Up(e, m, RelationType.ChildOf, _ => true)),
        [PartOfRelation.IfcRelVoidsFillsElement] = new("IFCRELVOIDSELEMENT IFCRELFILLSELEMENT", [RelationType.Fills, RelationType.Voids],
            (e, m) => Step(e, m, RelationType.Fills).SelectMany(o => Step(o.EntityIndex, m, RelationType.Voids))),
    };

    /// <summary>Part-of facets whose relation Xbim's loader did not recognise, with the relation
    /// as the IDS wrote it. The loader turns such a relation into an empty one, which reads as
    /// "any relation" and could pass an element through a relation the IDS never named.</summary>
    private static readonly ConditionalWeakTable<PartOfFacet, string> UnknownRelations = new();

    /// <summary>Pairs the part-of facets of <paramref name="ids"/>, in document order, with the
    /// <c>relation</c> attributes of the XML's partOf elements, and marks each facet whose
    /// relation was stated but came through empty. Returns a warning per marked facet. When
    /// the two counts differ, every part-of facet with an empty relation is marked, since a
    /// Pass through a relation nobody named is worse than InfoNotAvailable.</summary>
    internal static IReadOnlyList<string> MarkUnknownRelations(Xids ids, IReadOnlyList<string> relations)
    {
        var facets = ids.AllSpecifications()
            .SelectMany(s => IdsEvaluator.Facets(s.Applicability).Concat(IdsEvaluator.Facets(s.Requirement)))
            .OfType<PartOfFacet>()
            .ToList();
        var paired = facets.Count == relations.Count;
        if (paired && relations.All(string.IsNullOrWhiteSpace))
            return [];
        var warnings = new List<string>();
        for (var i = 0; i < facets.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(facets[i].EntityRelation))
                continue;
            var stated = paired ? relations[i] : "(unreadable)";
            if (string.IsNullOrWhiteSpace(stated))
                continue;
            UnknownRelations.AddOrUpdate(facets[i], stated);
            warnings.Add($"IDS loader: part-of relation '{stated}' is not an IDS 1.0 relation; the facet gives InfoNotAvailable");
        }
        return warnings;
    }

    /// <summary>The walk for the facet's relation, a union of all of them when it names none,
    /// or null when it names one this evaluator does not know.</summary>
    private static PartOfWalk? Walk(PartOfFacet facet)
    {
        if (UnknownRelations.TryGetValue(facet, out _))
            return null;
        if (string.IsNullOrWhiteSpace(facet.EntityRelation))
            return Any;
        var relation = facet.GetRelation();
        return Walks.GetValueOrDefault(relation);
    }

    /// <summary>With no relation stated: every walk. It needs no type up front; a container is
    /// found through the walks the model carries, and a miss is InfoNotAvailable when a walk was
    /// skipped (see <see cref="MatchPartOf"/>).</summary>
    private static readonly PartOfWalk Any = new(AnyRelation, [], (e, m) => Walks.Values
        .Where(w => w.Needs.All(m.Relations!.Carries))
        .SelectMany(w => w.Containers(e, m)));

    public static FacetMatch MatchPartOf(PartOfFacet facet, IdsEntity entity, IdsModel model, IdsWarnings warnings)
    {
        var walk = Walk(facet)!;
        var containers = walk.Containers(entity.EntityIndex, model).DistinctBy(c => c.EntityIndex).ToList();
        if (containers.Any(c => facet.EntityType is null || IdsFacets.MatchType(facet.EntityType, c).Satisfied))
            return FacetMatch.Yes;
        if (walk == Any && Walks.Values.FirstOrDefault(w => !w.Needs.All(model.Relations!.Carries)) is { } skipped)
        {
            var reason = $"part-of through {skipped.Name} cannot be told: the model has none of its relations";
            warnings.Add(reason);
            return FacetMatch.Unsupported(reason);
        }
        var subject = $"part of {facet.EntityType?.Short() ?? "a container"} through {walk.Name}";
        return containers.Count == 0
            ? FacetMatch.Absent($"not {subject}: no container")
            : FacetMatch.No($"not {subject}: containers are {string.Join(", ", containers.Select(c => $"{c.Category} '{c.Name}'"))}");
    }

    /// <summary>The direct targets of the entity's edges of one type.</summary>
    private static IEnumerable<IdsEntity> Step(long entity, IdsModel model, RelationType type)
        => model.Relations!.Targets(entity, type).Select(model.Entity).OfType<IdsEntity>();

    /// <summary>Every ancestor through edges of one type whose target passes the filter.</summary>
    private static IEnumerable<IdsEntity> Up(long entity, IdsModel model, RelationType type, Func<IdsEntity, bool> follow)
        => Reach(entity, model, [type], follow);

    /// <summary>Breadth-first from the entity through edges of the types, taking a target only
    /// when it passes the filter; each entity once, so a cycle ends.</summary>
    private static IEnumerable<IdsEntity> Reach(long start, IdsModel model, RelationType[] types, Func<IdsEntity, bool> follow)
    {
        var seen = new HashSet<long> { start };
        var queue = new Queue<long>([start]);
        while (queue.TryDequeue(out var current))
            foreach (var type in types)
                foreach (var target in model.Relations!.Targets(current, type))
                    if (seen.Add(target) && model.Entity(target) is { } entity && follow(entity))
                    {
                        queue.Enqueue(target);
                        yield return entity;
                    }
    }

    /// <summary>IfcGroup and its subtypes, the targets of IfcRelAssignsToGroup (the containers
    /// Xbim's PartOfFacet.Container lists, less IfcElementAssembly, which is aggregated).</summary>
    private static readonly HashSet<string> GroupClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "IFCGROUP", "IFCASSET", "IFCBUILDINGSYSTEM", "IFCBUILTSYSTEM", "IFCCONDITION", "IFCDISTRIBUTIONCIRCUIT",
        "IFCDISTRIBUTIONSYSTEM", "IFCELECTRICALCIRCUIT", "IFCINVENTORY", "IFCSTRUCTURALANALYSISMODEL",
        "IFCSTRUCTURALLOADCASE", "IFCSTRUCTURALLOADGROUP", "IFCSTRUCTURALRESULTGROUP", "IFCSYSTEM", "IFCZONE",
    };

    private static bool IsGroup(IdsEntity entity)
        => GroupClasses.Contains(entity.Category);
}
