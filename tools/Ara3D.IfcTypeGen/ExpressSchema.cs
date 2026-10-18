namespace Ara3D.IfcTypeGen;

/// <summary>
/// The parts of an EXPRESS schema (IFC or a STEP application protocol) that type generation needs.
/// </summary>
public record ExpressSchema(IReadOnlyList<ExpressEntity> Entities, IReadOnlyList<IExpressTypeDecl> Decls);

/// <summary>
/// An ENTITY declaration. Supertypes are in SUBTYPE OF order. Attributes are the explicit attributes the
/// entity declares itself; a SELF\x.y redeclaration in the explicit list narrows an inherited attribute and is
/// not among them. DerivedRedeclarations name the inherited explicit attributes that this entity computes in
/// its DERIVE section, which a Part 21 record writes as '*' in their usual position.
/// </summary>
public record ExpressEntity(
    string Name,
    IReadOnlyList<string> Supertypes,
    IReadOnlyList<ExpressAttribute> Attributes,
    IReadOnlyList<ExpressAttributeRef> DerivedRedeclarations);

/// <summary>SELF\Entity.Attribute: an attribute named through the supertype it is visible in.</summary>
public record ExpressAttributeRef(string Entity, string Attribute);

public record ExpressAttribute(string Name, ExpressTypeExpr TypeExpr);
public record ExpressTypeExpr(string Name, IReadOnlyList<ExpressTypeExpr> GenericParameters);

public interface IExpressTypeDecl;
public record ExpressEnumeration(string Name, IReadOnlyList<string> Options) : IExpressTypeDecl;
public record ExpressSelect(string Name, IReadOnlyList<string> Options) : IExpressTypeDecl;
public record ExpressTypeAlias(string Name, ExpressTypeExpr TypeExpr) : IExpressTypeDecl;
