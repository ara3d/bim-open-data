namespace Ara3D.Ifc.Conventions;

/// <summary>A material set class whose members each get their set and their 1-based position,
/// so a query can list a wall type's layers in order with their thicknesses.</summary>
/// <param name="SetClass">The set's IFC class.</param>
/// <param name="MembersAttribute">The STEP attribute index of the set's member list.</param>
/// <param name="SetParameter">Entity parameter on each member, naming the set.</param>
/// <param name="IndexParameter">Int parameter on each member, its 1-based position.</param>
public sealed record IfcMaterialSet(string SetClass, int MembersAttribute, string SetParameter, string IndexParameter);

public static class IfcMaterialSets
{
    public static readonly IReadOnlyList<IfcMaterialSet> All =
    [
        new("IFCMATERIALLAYERSET", 0, IfcParameterNames.LayerSet, IfcParameterNames.LayerIndex),
        new("IFCMATERIALCONSTITUENTSET", 2, IfcParameterNames.ConstituentSet, IfcParameterNames.ConstituentIndex),
    ];

    /// <summary>The material set of this class, or null when the class is not one.</summary>
    public static IfcMaterialSet? Of(string ifcClass)
        => All.FirstOrDefault(s => s.SetClass == ifcClass);
}
