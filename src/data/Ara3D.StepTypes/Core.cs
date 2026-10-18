namespace Ara3D.StepTypes;

/// <summary>Anything a STEP attribute can hold that has a generated interface: an entity or a SELECT.</summary>
public interface IStepValue;

/// <summary>The root of every generated entity interface. An entity's interface extends its supertypes'
/// interfaces, so "is this instance an X" is a type test against IX.</summary>
public interface IStepEntity : IStepValue;

/// <summary>The root of every generated SELECT interface; each entity a SELECT lists implements it.</summary>
public interface IStepSelect : IStepValue;

/// <summary>
/// The descriptor of one EXPRESS entity: its name as a Part 21 file writes it and one attribute per slot of its
/// simple-instance record, inherited slots included. Each generated subclass has one Instance and implements
/// the entity's interface.
/// </summary>
public abstract class StepEntityType : IStepEntity
{
    protected StepEntityType(string entityName)
        => EntityName = entityName;

    /// <summary>Upper case, as in ADVANCED_FACE.</summary>
    public string EntityName { get; }

    /// <summary>The slots of a simple instance's record, in record order.</summary>
    public abstract IReadOnlyList<StepAttribute> Attributes { get; }

    public override string ToString()
        => EntityName;
}

/// <summary>
/// One explicit attribute, addressed two ways. Position is its slot in the record of a simple instance of the
/// entity whose descriptor holds it. Entity and OwnPosition name the entity that declares it and its slot in that
/// entity's partial record, which is how a complex instance (NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.)) writes it and
/// how a simple instance of a subtype with several supertypes finds it. IsDerived marks a slot the file writes as *.
/// </summary>
public abstract class StepAttribute
{
    protected StepAttribute(string name, string entity, int position, int ownPosition, bool isDerived)
    {
        Name = name;
        Entity = entity;
        Position = position;
        OwnPosition = ownPosition;
        IsDerived = isDerived;
    }

    /// <summary>The EXPRESS name, as in face_geometry.</summary>
    public string Name { get; }

    /// <summary>The declaring entity, upper case, as in FACE_SURFACE.</summary>
    public string Entity { get; }

    public int Position { get; }
    public int OwnPosition { get; }
    public bool IsDerived { get; }

    public override string ToString()
        => $"{Entity}.{Name} Position={Position} OwnPosition={OwnPosition}{(IsDerived ? " derived" : "")}";
}

/// <summary>An attribute whose value has C# type T: double, long, bool, string, a generated interface or enum,
/// or an IReadOnlyList of one of those.</summary>
public sealed class StepAttribute<T> : StepAttribute
{
    public StepAttribute(string name, string entity, int position, int ownPosition, bool isDerived = false)
        : base(name, entity, position, ownPosition, isDerived)
    { }
}

public static class StepEntityTypeExtensions
{
    /// <summary>
    /// The attribute's slot in a simple instance of this entity, or -1 when this entity's record has no such
    /// attribute. Equal to attribute.Position when the attribute belongs to this entity's descriptor; for a
    /// subtype it is found by declaring entity and OwnPosition, because a second supertype's slots move.
    /// </summary>
    public static int PositionOf(this StepEntityType type, StepAttribute attribute)
    {
        var slots = type.Attributes;
        if (attribute.Position < slots.Count && ReferenceEquals(slots[attribute.Position], attribute))
            return attribute.Position;
        for (var i = 0; i < slots.Count; i++)
            if (slots[i].OwnPosition == attribute.OwnPosition && slots[i].Entity == attribute.Entity)
                return i;
        return -1;
    }
}
