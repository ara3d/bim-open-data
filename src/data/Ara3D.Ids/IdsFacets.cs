using Xbim.InformationSpecifications;

namespace Ara3D.Ids;

/// <summary>How one facet matched one entity. <see cref="Present"/> says whether the facet's
/// subject exists at all (the property, the attribute, the class); <see cref="Satisfied"/> whether
/// every constraint on it holds. An unsupported facet is neither.</summary>
internal readonly record struct FacetMatch(bool Supported, bool Present, bool Satisfied, string Reason)
{
    public static FacetMatch Unsupported(string reason) => new(false, false, false, reason);
    public static FacetMatch Yes => new(true, true, true, "");
    public static FacetMatch Absent(string reason) => new(true, false, false, reason);
    public static FacetMatch No(string reason) => new(true, true, false, reason);
}

/// <summary>Warnings raised once each while an IDS is evaluated.</summary>
internal sealed class IdsWarnings
{
    private readonly List<string> _list = [];
    private readonly HashSet<string> _seen = [];

    public void Add(string warning)
    {
        if (_seen.Add(warning))
            _list.Add(warning);
    }

    public IReadOnlyList<string> List => _list;
}

/// <summary>Matches the facets BOS can answer: entity (class and predefined type), property, and
/// attribute. Classification, material, part-of and document facets need relations BOS does not
/// carry in a form the evaluator reads yet, and produce <see cref="FacetMatch.Unsupported"/>.</summary>
internal static class IdsFacets
{
    public static bool IsSupported(IFacet facet)
        => facet is IfcTypeFacet or IfcPropertyFacet or AttributeFacet;

    public static FacetMatch Match(IFacet facet, IdsEntity entity, IdsWarnings warnings)
        => facet switch
        {
            IfcTypeFacet f => MatchType(f, entity, warnings),
            IfcPropertyFacet f => MatchProperty(f, entity, warnings),
            AttributeFacet f => MatchAttribute(f, entity),
            _ => FacetMatch.Unsupported(Unsupported(facet, warnings)),
        };

    private static string Unsupported(IFacet facet, IdsWarnings warnings)
    {
        var reason = $"{facet.GetType().Name.Replace("Facet", "").Replace("Ifc", "").ToLowerInvariant()} facets are not evaluated over BOS";
        warnings.Add(reason + $" ({facet.Short()})");
        return reason;
    }

    private static FacetMatch MatchType(IfcTypeFacet facet, IdsEntity entity, IdsWarnings warnings)
    {
        if (facet.IncludeSubtypes)
            warnings.Add($"IncludeSubtypes is ignored: the entity facet '{facet.Short()}' matches the class exactly");
        if (!Satisfies(facet.IfcType, entity.Category, ignoreCase: true))
            return FacetMatch.Absent($"class {entity.Category} is not {facet.IfcType?.Short()}");
        if (IsEmpty(facet.PredefinedType))
            return FacetMatch.Yes;
        var predefined = entity.Attribute("PredefinedType");
        if (predefined is { Token: "USERDEFINED" })
            predefined = entity.Attribute("ObjectType");
        return predefined is { IsPresent: true } && Satisfies(facet.PredefinedType, predefined.Token, ignoreCase: false)
            ? FacetMatch.Yes
            : FacetMatch.No($"PredefinedType '{predefined?.Token}' is not {facet.PredefinedType!.Short()}");
    }

    /// <summary>Raised once per report whenever a property facet is evaluated, until TKT-237
    /// (bim-open-toolkit) brings type-object property sets into BOS.</summary>
    internal const string TypePropertySetsWarning =
        "property sets on type objects are not in BOS yet, so a value an occurrence inherits from its type is not seen (TKT-237)";

    private static FacetMatch MatchProperty(IfcPropertyFacet facet, IdsEntity entity, IdsWarnings warnings)
    {
        warnings.Add(TypePropertySetsWarning);
        var candidates = entity.Properties
            .Where(v => Satisfies(facet.PropertySetName, v.Group, ignoreCase: false)
                && Satisfies(facet.PropertyName, v.Name, ignoreCase: false) && v.IsPresent)
            .ToList();
        var subject = $"{facet.PropertySetName?.Short()}.{facet.PropertyName?.Short()}";
        if (candidates.Count == 0)
            return FacetMatch.Absent($"no {subject}");
        var expected = ExpectedKind(facet.DataType, warnings);
        foreach (var value in candidates)
        {
            if (expected is not IdsValueKind.Unknown && value.Kind != expected)
                return FacetMatch.No($"{subject} = '{value.Text}' is {Describe(value.Kind)}, not {facet.DataType}");
            if (!IsEmpty(facet.PropertyValue) && !Satisfies(facet.PropertyValue, value.Candidate, ignoreCase: false))
                return FacetMatch.No($"{subject} = '{value.Text}' is not {facet.PropertyValue!.Short()}");
        }
        return FacetMatch.Yes;
    }

    private static FacetMatch MatchAttribute(AttributeFacet facet, IdsEntity entity)
    {
        var candidates = entity.Attributes
            .Where(a => Satisfies(facet.AttributeName, a.Name, ignoreCase: false) && a.Value.IsPresent)
            .ToList();
        var subject = $"attribute {facet.AttributeName?.Short()}";
        if (candidates.Count == 0)
            return FacetMatch.Absent($"no {subject}");
        foreach (var (name, value) in candidates)
            if (!IsEmpty(facet.AttributeValue) && !Satisfies(facet.AttributeValue, value.Candidate, ignoreCase: false))
                return FacetMatch.No($"{name} = '{value.Text}' is not {facet.AttributeValue!.Short()}");
        return FacetMatch.Yes;
    }

    /// <summary>The kind an IDS <c>dataType</c> names. BOS keeps numbers, logicals and text apart
    /// but not IFC's measure types, so IFCLENGTHMEASURE and IFCAREAMEASURE both mean a number.</summary>
    internal static IdsValueKind ExpectedKind(string? dataType, IdsWarnings warnings)
    {
        if (string.IsNullOrWhiteSpace(dataType))
            return IdsValueKind.Unknown;
        var type = dataType.Trim().ToUpperInvariant();
        if (type is "IFCBOOLEAN" or "IFCLOGICAL")
            return IdsValueKind.Boolean;
        if (type is "IFCLABEL" or "IFCTEXT" or "IFCIDENTIFIER" or "IFCDATE" or "IFCDATETIME" or "IFCTIME"
            or "IFCDURATION" or "IFCTIMESTAMP" or "IFCGLOBALLYUNIQUEID" or "IFCURIREFERENCE" or "IFCDESCRIPTIVEMEASURE")
            return IdsValueKind.Text;
        if (type.EndsWith("MEASURE", StringComparison.Ordinal)
            || type is "IFCREAL" or "IFCINTEGER" or "IFCNUMBER" or "IFCPOSITIVEINTEGER")
            return IdsValueKind.Number;
        warnings.Add($"dataType {dataType} is not checked: BOS does not record it");
        return IdsValueKind.Unknown;
    }

    private static string Describe(IdsValueKind kind)
        => kind switch
        {
            IdsValueKind.Number => "a number",
            IdsValueKind.Boolean => "a logical",
            _ => "text",
        };

    internal static bool IsEmpty(ValueConstraint? constraint)
        => constraint is null || ValueConstraint.IsEmpty(constraint);

    /// <summary>An empty constraint accepts anything, as IDS reads an omitted value.</summary>
    internal static bool Satisfies(ValueConstraint? constraint, object candidate, bool ignoreCase)
        => IsEmpty(constraint) || constraint!.IsSatisfiedBy(candidate, ignoreCase, null);
}
