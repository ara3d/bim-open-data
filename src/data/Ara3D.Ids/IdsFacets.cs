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

/// <summary>Matches the facets BOS can answer: entity (class and predefined type), property,
/// attribute, and, when the model has its relations, material and part-of (see
/// <see cref="IdsRelationFacets"/>). Classification and document facets, and the two relation
/// facets over a model without relations, produce <see cref="FacetMatch.Unsupported"/>.</summary>
internal static class IdsFacets
{
    /// <summary>True when the facet can be evaluated over a model that has its relations;
    /// <see cref="Unsupported(IFacet, IdsModel)"/> answers for a given model.</summary>
    public static bool IsSupported(IFacet facet)
        => facet is IfcTypeFacet or IfcPropertyFacet or AttributeFacet || IdsRelationFacets.IsSupported(facet);

    /// <summary>Why the facet cannot be evaluated over this model at all, or null when it can.</summary>
    public static string? Unsupported(IFacet facet, IdsModel model)
        => facet switch
        {
            IfcTypeFacet or IfcPropertyFacet or AttributeFacet => null,
            MaterialFacet or PartOfFacet => IdsRelationFacets.Unsupported(facet, model),
            _ => $"{FacetName(facet)} facets are not evaluated over BOS",
        };

    /// <summary>"material" for a MaterialFacet, "partof" for a PartOfFacet.</summary>
    public static string FacetName(IFacet facet)
        => facet.GetType().Name.Replace("Facet", "").Replace("Ifc", "").ToLowerInvariant();

    /// <summary>Numbers are compared in SI, as IDS states them, using the model's recorded
    /// units (see <see cref="IdsUnits"/>).</summary>
    public static FacetMatch Match(IFacet facet, IdsEntity entity, IdsModel model, IdsWarnings warnings)
    {
        if (Unsupported(facet, model) is { } reason)
        {
            warnings.Add(reason + $" ({facet.Short()})");
            return FacetMatch.Unsupported(reason);
        }
        return facet switch
        {
            IfcTypeFacet f => MatchType(f, entity),
            IfcPropertyFacet f => MatchProperty(f, entity, model.Units, warnings),
            AttributeFacet f => MatchAttribute(f, entity, model.Units, warnings),
            MaterialFacet f => IdsRelationFacets.MatchMaterial(f, entity, model, warnings),
            PartOfFacet f => IdsRelationFacets.MatchPartOf(f, entity, model, warnings),
            _ => throw new InvalidOperationException($"no matcher for {facet.GetType().Name}"),
        };
    }

    /// <summary>The class matches exactly, as IDS 1.0 defines the entity facet: its schema gives
    /// the facet a name and a predefined type and no subtype flag, so IFCWALL does not select an
    /// IFCWALLSTANDARDCASE and an IDS lists every class it means. <see cref="IfcTypeFacet.IncludeSubtypes"/>
    /// is an xbim extension ("not part of buildingSmart specification") that the IDS XML loader
    /// never sets, and is not read here.</summary>
    internal static FacetMatch MatchType(IfcTypeFacet facet, IdsEntity entity)
    {
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

    /// <summary>Raised once per report when an occurrence names a type object the model cannot
    /// find by name, so a property or material it lacks gives InfoNotAvailable.</summary>
    internal const string UnresolvedTypeWarning =
        "an occurrence's type object cannot be told from its name in the entities table, so a property or material it might inherit gives InfoNotAvailable; a TypeIndex column (the type's EntityIndex) removes the doubt";

    /// <summary>InfoNotAvailable, with <see cref="UnresolvedTypeWarning"/>, when the entity names a
    /// type object that cannot be found, so what the type might give it cannot be told; null when
    /// its type is found or it names none. <paramref name="lacking"/> says what the entity's own
    /// reading lacks ("no material of its own").</summary>
    internal static FacetMatch? UnresolvedType(IdsEntity entity, string lacking, IdsWarnings warnings)
    {
        if (entity.TypeUnresolved is not { } unresolved)
            return null;
        warnings.Add(UnresolvedTypeWarning);
        return FacetMatch.Unsupported($"{lacking}, and {unresolved}");
    }

    /// <summary>Reads the entity's own values and those it inherits from its type. When it names a
    /// type that cannot be found and carries no matching value itself, the answer cannot be told.</summary>
    private static FacetMatch MatchProperty(IfcPropertyFacet facet, IdsEntity entity, IdsUnitScales units, IdsWarnings warnings)
    {
        var candidates = entity.Properties
            .Where(v => Satisfies(facet.PropertySetName, v.Group, ignoreCase: false)
                && Satisfies(facet.PropertyName, v.Name, ignoreCase: false) && v.IsPresent)
            .ToList();
        var subject = $"{facet.PropertySetName?.Short()}.{facet.PropertyName?.Short()}";
        if (candidates.Count == 0 && UnresolvedType(entity, $"no {subject} of its own", warnings) is { } unknown)
            return unknown;
        if (candidates.Count == 0)
            return FacetMatch.Absent($"no {subject}");
        var expected = ExpectedKind(facet.DataType, warnings);
        var power = IdsUnits.LengthPower(facet.DataType);
        foreach (var value in candidates)
        {
            if (expected is not IdsValueKind.Unknown && value.Kind != expected)
                return FacetMatch.No($"{subject} = '{value.Text}' is {Describe(value.Kind)}, not {facet.DataType}");
            if (IsEmpty(facet.PropertyValue))
                continue;
            var candidate = IdsUnits.Candidate(value, power, units, $"{value.Group}.{value.Name}", warnings);
            if (!Satisfies(facet.PropertyValue, candidate, ignoreCase: false))
                return FacetMatch.No($"{subject} = {Shown(value, candidate)} is not {facet.PropertyValue!.Short()}");
        }
        return FacetMatch.Yes;
    }

    private static FacetMatch MatchAttribute(AttributeFacet facet, IdsEntity entity, IdsUnitScales units, IdsWarnings warnings)
    {
        var candidates = entity.Attributes
            .Where(a => Satisfies(facet.AttributeName, a.Name, ignoreCase: false) && a.Value.IsPresent)
            .ToList();
        var subject = $"attribute {facet.AttributeName?.Short()}";
        if (candidates.Count == 0)
            return FacetMatch.Absent($"no {subject}");
        if (IsEmpty(facet.AttributeValue))
            return FacetMatch.Yes;
        foreach (var (name, value) in candidates)
        {
            var candidate = IdsUnits.Candidate(value, IdsUnits.AttributeLengthPower(name), units, $"attribute {name}", warnings);
            if (!Satisfies(facet.AttributeValue, candidate, ignoreCase: false))
                return FacetMatch.No($"{name} = {Shown(value, candidate)} is not {facet.AttributeValue!.Short()}");
        }
        return FacetMatch.Yes;
    }

    /// <summary>The stored text, and the SI number it was compared as when that differs.</summary>
    private static string Shown(IdsValue value, object candidate)
        => candidate is double number && !Equals(candidate, value.Candidate)
            ? $"'{value.Text}' ({number.ToString("G6", System.Globalization.CultureInfo.InvariantCulture)} in SI)"
            : $"'{value.Text}'";

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
