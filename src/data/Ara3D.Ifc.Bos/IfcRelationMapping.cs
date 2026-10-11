using System;
using Ara3D.IfcLoader;

namespace Ara3D.BimOpenSchema.IO;

/// <summary>Turns Ara3D.IfcLoader's relation kinds into BOS relation types. It stays here, not in
/// Ara3D.Ifc.Conventions, because IfcRelationKind belongs to the Windows-only loader; the IFC
/// classes behind each kind are listed in IfcRelations.ParseEntity, and the ones a reader of
/// inverse attributes also sees are in Ara3D.Ifc.Conventions.IfcRelationNames.</summary>
public static class IfcRelationMapping
{
    public static RelationType ToBos(IfcRelationKind kind)
        => kind switch
        {
            IfcRelationKind.ContainedIn => RelationType.ContainedIn,
            IfcRelationKind.MemberOf => RelationType.MemberOf,
            IfcRelationKind.ChildOf => RelationType.ChildOf,
            IfcRelationKind.PartOf => RelationType.PartOf,
            IfcRelationKind.HasMaterial => RelationType.HasMaterial,
            IfcRelationKind.HasLayer => RelationType.HasLayer,
            IfcRelationKind.Voids => RelationType.Voids,
            IfcRelationKind.Fills => RelationType.Fills,
            IfcRelationKind.ConnectsTo => RelationType.ConnectsTo,
            IfcRelationKind.HasConnector => RelationType.HasConnector,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
