using Ara3D.IO.StepParser;
using Ara3D.StepTypes;

namespace Ara3D.Step.Entities;

/// <summary>One partial entity of an instance and its descriptor; Type is null for a name AP242 does not declare.</summary>
public readonly record struct StepPart(StepPartialEntity Partial, StepEntityType? Type);

/// <summary>
/// One instance of a STEP file, simple (#1=CARTESIAN_POINT(...)) or complex (#2=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(...))).
/// It answers membership and reads attributes the same way for both: a complex instance is an X when any of its
/// parts is X or a subtype of X, and an attribute is read from the part of the entity that declares it.
/// </summary>
public readonly struct StepEntity
{
    public StepEntity(StepModel model, StepDefinition definition)
    {
        Model = model;
        Definition = definition;
        Type = definition.IsComplex() ? null : StepSchemaIndex.Find(definition.NameToken);
    }

    public StepModel Model { get; }
    public StepDefinition Definition { get; }

    /// <summary>The descriptor of a simple instance; null for a complex instance or an undeclared name.</summary>
    public StepEntityType? Type { get; }

    public int Id
        => Definition.Id;

    public bool IsComplex
        => Definition.IsComplex();

    /// <summary>The partial entities in file order; a simple instance has one.</summary>
    public IReadOnlyList<StepPart> Parts
        => Definition.PartialEntities(Model.Document).Select(p => new StepPart(p, StepSchemaIndex.Find(p.NameToken))).ToList();

    /// <summary>True when every part's name is declared by AP242.</summary>
    public bool IsKnown
        => IsComplex ? Parts.All(p => p.Type != null) : Type != null;

    /// <summary>True when the instance is a T: e.Is&lt;IBSplineSurface&gt;() holds for a simple
    /// B_SPLINE_SURFACE_WITH_KNOTS and for a complex instance with a RATIONAL_B_SPLINE_SURFACE part.</summary>
    public bool Is<T>() where T : IStepEntity
        => IsComplex ? Parts.Any(p => p.Type is T) : Type is T;

    /// <summary>
    /// The token in the attribute's slot, which may be $ or *. False when the instance has no such attribute: a
    /// simple instance of an entity whose record lacks it, a complex instance without the declaring part, or a
    /// record shorter than the slot.
    /// </summary>
    public bool TryGet(StepAttribute attribute, out StepToken value)
    {
        if (!IsComplex)
        {
            var position = Type?.PositionOf(attribute) ?? -1;
            value = default;
            return position >= 0 && Definition.AttributesToken.TryElementAt(Model.Document, position, out value);
        }
        foreach (var part in Definition.PartialEntities(Model.Document))
            if (part.NameToken.Span.MatchesName(attribute.Entity))
                return part.AttributesToken.TryElementAt(Model.Document, attribute.OwnPosition, out value);
        value = default;
        return false;
    }

    public override string ToString()
        => $"#{Id}={(IsComplex ? "(" + string.Join("", Parts.Select(p => p.Partial.NameToken.ToString())) + ")" : Definition.NameToken.ToString())}";
}
