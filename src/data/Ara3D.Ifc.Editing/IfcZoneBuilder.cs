namespace Ara3D.Ifc.Tests;

/// <summary>
/// Builds the STEP lines that group existing spaces into a zone: one IfcZone and one
/// IfcRelAssignsToGroup that lists the spaces. Ids come from <see cref="NextId"/> and GlobalIds from
/// the zone's name (or a caller-supplied key), so repeated runs produce identical bytes. The zone is
/// an object like any other: a property set attaches to the id <see cref="AddZone"/> returns, with
/// <see cref="IfcPropertySetBuilder"/>. Attribute lists follow the schema given to the constructor.
/// </summary>
public sealed class IfcZoneBuilder
{
    public readonly int OwnerHistoryId;
    public readonly IfcSchema Schema;

    private readonly IfcStepLines _out;

    public IfcZoneBuilder(int firstNewId, int ownerHistoryId, IfcSchema schema)
    {
        _out = new IfcStepLines(firstNewId);
        OwnerHistoryId = ownerHistoryId;
        Schema = schema;
    }

    public int NextId
    {
        get => _out.NextId;
        set => _out.NextId = value;
    }

    public IReadOnlyList<string> Lines
        => _out.Lines;

    /// <summary>Every id allocated so far, in allocation order.</summary>
    public IReadOnlyList<int> Ids
        => _out.Ids;

    /// <summary>
    /// Appends the zone and the relationship that assigns <paramref name="spaceIds"/> to it, and
    /// returns the zone's id. <paramref name="guidKey"/> defaults to the zone name, so a zone keeps
    /// its GlobalId when the table around it changes.
    /// </summary>
    public int AddZone(string name, IReadOnlyList<int> spaceIds, string? description = null, string? guidKey = null)
    {
        if (spaceIds.Count == 0)
            throw new ArgumentException($"Zone '{name}' has no spaces", nameof(spaceIds));
        var key = guidKey ?? "zone:" + name;

        var zoneId = _out.Emit("IFCZONE", ZoneAttributes(name, description, key));
        _out.Emit("IFCRELASSIGNSTOGROUP", IfcStepLines.Attributes(
            IfcStepLines.GuidLiteral(key + ":rel"),
            $"#{OwnerHistoryId}",
            IfcStepText.String(name),
            IfcStepLines.OptionalString(description),
            IfcStepText.IdList(spaceIds),
            "$",
            $"#{zoneId}"));
        return zoneId;
    }

    /// <summary>
    /// GlobalId, OwnerHistory, Name, Description, ObjectType; IFC4 adds LongName, left absent.
    /// </summary>
    private string ZoneAttributes(string name, string? description, string key)
    {
        var common = new[]
        {
            IfcStepLines.GuidLiteral(key), $"#{OwnerHistoryId}", IfcStepText.String(name),
            IfcStepLines.OptionalString(description), "$",
        };
        return Schema == IfcSchema.Ifc2x3
            ? IfcStepLines.Attributes(common)
            : IfcStepLines.Attributes([.. common, "$"]);
    }
}
