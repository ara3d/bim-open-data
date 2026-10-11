using Ara3D.BimOpenSchema;

namespace Ara3D.Ifc.Conventions;

/// <summary>Rules a reader applies by IFC class name, written upper case as STEP spells it
/// (<c>IFCSPACE</c>), the form both the STEP parser and That Open's Fragments categories use.</summary>
public static class IfcClasses
{
    /// <summary>Classes whose geometry instances are flagged hidden: spatial containers, zones,
    /// grids, and annotations, which would otherwise hide or clutter the building in a 3D view.
    /// The entities themselves, and their relations, are kept.</summary>
    public static readonly IReadOnlySet<string> Hidden = new HashSet<string>(
    [
        "IFCSITE",
        "IFCBUILDING",
        "IFCBUILDINGSTOREY",
        "IFCSPACE",
        "IFCSPATIALZONE",
        "IFCZONE",
        "IFCGRID",
        "IFCGRIDAXIS",
        "IFCANNOTATION",
        "IFCVIRTUALGRIDINTERSECTION",
    ], StringComparer.Ordinal);

    /// <summary>Geometry, placement, and property-definition classes a STEP reader does not make
    /// entities of. Relation classes (<c>IFCREL...</c>) are left out by
    /// <see cref="IsMaybeElement"/> as well.</summary>
    public static readonly IReadOnlySet<string> NonElement = new HashSet<string>(
    [
        "IFCINDEXEDPOLYGONALFACE",
        "IFCPOLYLOOP",
        "IFCFACE",
        "IFCFACEOUTERBOUND",
        "IFCCARTESIANPOINT",
        "IFCRELDEFINESBYPROPERTIES",
        "IFCPROPERTYSET",
        "IFCPROPERTYSINGLEVALUE",
        "IFCAXIS2PLACEMENT3D",
        "IFCSHAPEREPRESENTATION",
        "IFCQUANTITYLENGTH",
        "IFCSTYLEDITEM",
        "IFCLOCALPLACEMENT",
        "IFCQUANTITYAREA",
        "IFCPRODUCTDEFINITIONSHAPE",
        "IFCCARTESIANPOINTLIST3D",
        "IFCMAPPEDITEM",
        "IFCPOLYGONALFACESET",
        "IFCINDEXEDPOLYCURVE",
        "IFCQUANTITYVOLUME",
        "IFCELEMENTQUANTITY",
        "IFCCARTESIANPOINTLIST2D",
        "IFCEXTRUDEDAREASOLID",
        "IFCINDEXEDCOLOURMAP",
        "IFCCOLOURRGBLIST",
        "IFCARBITRARYCLOSEDPROFILEDEF",
        "IFCDIRECTION",
    ], StringComparer.Ordinal);

    public static bool IsHidden(string? ifcClass)
        => ifcClass != null && Hidden.Contains(ifcClass);

    /// <summary>Whether an entity of this class becomes a BOS entity: not a relation and not in
    /// <see cref="NonElement"/>.</summary>
    public static bool IsMaybeElement(string ifcClass)
        => !NonElement.Contains(ifcClass) && !ifcClass.StartsWith("IFCREL", StringComparison.Ordinal);

    /// <summary>IfcMaterial, its layers, constituents, profiles, and the sets and usages that group
    /// them. None is an IfcRoot, so their first attributes are what they are about, not GlobalId,
    /// OwnerHistory, and Name.</summary>
    public static bool IsMaterialResource(string ifcClass)
        => ifcClass.StartsWith("IFCMATERIAL", StringComparison.Ordinal);

    /// <summary>The BOS flags of a geometry instance of an entity of this class: exactly
    /// <see cref="BimGeometry.InstanceFlagEnum.IsHidden"/> when the class is hidden, otherwise the
    /// flags the geometry source gave.</summary>
    public static byte InstanceFlags(string? ifcClass, byte sourceFlags)
        => IsHidden(ifcClass) ? (byte)BimGeometry.InstanceFlagEnum.IsHidden : sourceFlags;
}
