using Xbim.InformationSpecifications;
using Xbim.InformationSpecifications.Cardinality;
using Cardinality = Xbim.InformationSpecifications.RequirementCardinalityOptions.Cardinality;

namespace Ara3D.Ids;

/// <summary>Evaluates every specification of an IDS over a model: the applicability facets select
/// the entities, the requirement facets judge each one, and each entity gets one verdict row per
/// specification. A specification whose cardinality the model breaks (a required one that applies
/// to nothing) gets a model-level row with an empty GlobalId. Facets BOS cannot answer give
/// <see cref="Verdict.InfoNotAvailable"/> and a warning, never a pass.</summary>
public static class IdsEvaluator
{
    /// <summary>Evaluates a loaded document; the loader's warnings (a facet it skipped, say)
    /// come first in the report's warnings, because a skipped facet is a requirement nobody checked.</summary>
    public static IdsReport Evaluate(IdsDocument document, IdsModel model)
        => Evaluate(document.Ids, model, document.Warnings);

    public static IdsReport Evaluate(Xids ids, IdsModel model, IReadOnlyList<string>? loaderWarnings = null)
    {
        var warnings = new IdsWarnings();
        foreach (var warning in loaderWarnings ?? [])
            warnings.Add(warning);
        var verdicts = new List<IdsVerdict>();
        var index = 0;
        foreach (var specification in ids.AllSpecifications())
            Evaluate(specification, index++, model, verdicts, warnings);
        return new(verdicts, warnings.List);
    }

    /// <summary>The requirement facets as the IDS states them, the row's citation.</summary>
    public static string Citation(Specification specification)
        => string.Join("; ", Facets(specification.Requirement).Select(f => f.Short()));

    public static string CheckId(Specification specification)
        => specification.Name is { Length: > 0 } name ? name : specification.Guid ?? "";

    public static string CheckTitle(Specification specification)
        => specification.Description is { Length: > 0 } description ? description : CheckId(specification);

    internal static IReadOnlyList<IFacet> Facets(FacetGroup? group)
        => group?.Facets?.ToList() ?? [];

    private static void Evaluate(Specification specification, int index, IdsModel model, List<IdsVerdict> verdicts, IdsWarnings warnings)
    {
        var checkId = CheckId(specification);
        var title = CheckTitle(specification);
        var citation = Citation(specification);
        var applicability = Facets(specification.Applicability);
        var requirements = Facets(specification.Requirement);

        void Row(IdsEntity? entity, Outcome outcome)
            => verdicts.Add(new(entity?.GlobalId ?? "", entity?.EntityIndex ?? -1, checkId, title, citation, outcome.Verdict, outcome.Reason, index));

        if (applicability.Any(f => !IdsFacets.IsSupported(f)))
        {
            foreach (var facet in applicability.Where(f => !IdsFacets.IsSupported(f)))
                IdsFacets.Match(facet, model.Entities.FirstOrDefault() ?? Empty, model.LengthToMetre, warnings);
            Row(null, Outcome.Unknown("the applicability uses a facet that is not evaluated over BOS"));
            return;
        }

        var applicable = model.Entities
            .Where(e => applicability.All(f => IdsFacets.Match(f, e, model.LengthToMetre, warnings).Satisfied))
            .ToList();

        if (IsProhibited(specification.Cardinality))
        {
            foreach (var entity in applicable)
                Row(entity, Outcome.Fail("matches a prohibited specification"));
            return;
        }

        if (specification.Cardinality is { } cardinality && !cardinality.IsSatisfiedBy(applicable.Count))
            Row(null, Outcome.Fail($"{applicable.Count} applicable entities; the specification is {cardinality.Description}"));

        foreach (var entity in applicable)
            Row(entity, requirements
                .Select(f => Judge(f, RequirementCardinality(specification.Requirement, f), entity, model.LengthToMetre, warnings))
                .ToList()
                .Combine());
    }

    private static readonly IdsEntity Empty = new(-1, "", "", "", []);

    /// <summary>Prohibited at the specification level: no entity may match the applicability.</summary>
    private static bool IsProhibited(ICardinality? cardinality)
        => cardinality is SimpleCardinality { ApplicabilityCardinality: CardinalityEnum.Prohibited }
            or MinMaxCardinality { MaxOccurs: 0 };

    private static Cardinality RequirementCardinality(FacetGroup? requirement, IFacet facet)
    {
        if (requirement is null)
            return Cardinality.Expected;
        requirement.GetRequirementCardinalityOption(facet, out var cardinality);
        return cardinality ?? Cardinality.Expected;
    }

    /// <summary>One facet's outcome under its cardinality: expected means the facet must hold,
    /// prohibited that it must not, optional that it must hold only when its subject exists.</summary>
    private static Outcome Judge(IFacet facet, Cardinality cardinality, IdsEntity entity, double? lengthToMetre, IdsWarnings warnings)
    {
        var match = IdsFacets.Match(facet, entity, lengthToMetre, warnings);
        if (!match.Supported)
            return Outcome.Unknown(match.Reason);
        return cardinality switch
        {
            Cardinality.Prohibited => match.Satisfied ? Outcome.Fail($"prohibited: {facet.Short()}") : Outcome.Pass,
            Cardinality.Optional => !match.Present || match.Satisfied ? Outcome.Pass : Outcome.Fail(match.Reason),
            _ => match.Satisfied ? Outcome.Pass : Outcome.Fail(match.Reason),
        };
    }
}
