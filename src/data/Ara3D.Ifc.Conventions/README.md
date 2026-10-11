# Ara3D.Ifc.Conventions

The IFC rules every reader that turns IFC content into BIM Open Schema (BOS)
data shares, so two readers of the same building write the same BOS. It targets
net8.0 and references only the `Ara3D.BimOpenSchema` spec project: no IFC
loader, no native code.

Its users are `Ara3D.Ifc.Bos` (IFC files through the Windows-only web-ifc
loader) and `Ara3D.BimOpenSchema.IO.Fragments` (That Open `.frag` files,
portable).

| Type | Holds |
|---|---|
| `IfcParameterNames` | `Ifc:<attribute>` and the named parameters: `Ifc:Room:Number`, `Ifc:AxisTag`, `Ifc:LengthUnit`, `Ifc:LengthUnitToMetre`, `Ifc:LayerSet`, `Ifc:LayerIndex`, `Ifc:ConstituentSet`, `Ifc:ConstituentIndex`; the IfcRoot attributes that are not parameters |
| `IfcClasses` | Classes whose geometry is flagged hidden (sites, buildings, storeys, spaces, zones, grids, annotations) and the flag rule; classes that never become entities; the material resource family |
| `IfcMaterialSets` | Layer and constituent sets: where the member list is, and the parameters each member gets |
| `IfcRelationNames` | IFC relation classes that become BOS relations, with the inverse attribute names that reach them; the type and property-set names a reader of inverse attributes follows |
| `IfcPropertyValues` | The attributes holding a property's or quantity's value, and the IFC defined types that are integers |

Class names are upper case, as STEP writes them and Fragments categories
repeat them (`IFCSPACE`). `tests/data/Ara3D.Ifc.Conventions.Tests` pins every
table, so a change to one is a deliberate edit to its test.
