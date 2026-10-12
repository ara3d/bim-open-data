# Ara3D.Ifc.Bos

The IFC to BIM Open Schema conversion path (`IfcToBosConverter`,
`IfcRelationMapping`), moved out of `Ara3D.BimOpenSchema.IO`.

It exists to break a dependency cycle at the group level. `Ara3D.IfcLoader`
referenced `Ara3D.BimOpenSchema`, and `Ara3D.BimOpenSchema.IO` referenced
`Ara3D.IfcLoader`, so the schema could not be built or shipped without dragging
in the native web-ifc loader. The converter is the only code that genuinely
needed both sides, so it lives here instead, referencing
`Ara3D.BimOpenSchema.IO` and `Ara3D.IfcLoader`. `Ara3D.BimOpenSchema` and
`Ara3D.BimOpenSchema.IO` now have no path to any IFC project.

The classes keep their original `Ara3D.BimOpenSchema.IO` namespace so existing
consumers work unchanged; only a project reference has to be added.

The IFC rules a reader needs that do not depend on the loader (parameter names
such as `Ifc:<attribute>` and `Ifc:LayerSet`, the classes flagged hidden, the
classes that never become entities, the material sets) live in the portable
`Ara3D.Ifc.Conventions` (net8.0), which the Fragments reader shares.
`IfcToBosConverter.AxisTagParameter`, `ToIfcStdPropName`, and
`IfcLengthUnit.NameParameter` / `ScaleParameter` forward to it for existing
callers. `IfcRelationMapping` stays here: it maps `Ara3D.IfcLoader`'s
`IfcRelationKind`, which the portable project cannot see.

## Length unit and grid axis tag (TKT-30 C2)

`IfcLengthUnit.Read(IfcFile)` resolves the project's `LENGTHUNIT`: the
`UnitsInContext` of the file's `IFCPROJECT`. It returns `null` when the file
declares none.

- For an `IfcSIUnit`, the result combines the SI prefix (if any) with
  `METRE`, e.g. no prefix gives `("METRE", 1.0)`, and `.MILLI.,.METRE.` gives
  `("MILLIMETRE", 0.001)`.
- For an `IfcConversionBasedUnit` (or `IfcConversionBasedUnitWithOffset`),
  the result is the unit's own name (e.g. `'FOOT'`) and its
  `IfcMeasureWithUnit` factor multiplied by the `ToMetre` of the SI unit it
  is defined against. Snowdon Structural's `FOOT` unit resolves this way to
  `("FOOT", 0.3048)`.

`IfcToBosConverter` writes the result as two parameters on the `IFCPROJECT`
entity: `Ifc:LengthUnit` (String) and `Ifc:LengthUnitToMetre` (Number), both
in group `IFCPROJECT`. A file with no length unit gets neither parameter.

`IfcToBosConverter` also writes `Ifc:AxisTag` (String, group `IFCGRIDAXIS`)
on each `IFCGRIDAXIS` entity, taken from attribute 0. `IFCGRIDAXIS` is not an
`IfcRoot`, so its attribute 0 is the tag rather than a `GlobalId`; the
converter's generic property loop (`ProcessAttributeAsProp`) assumes
`GlobalId`/`OwnerHistory`/`Name` occupy attributes 0-2 and starts reading
properties at attribute 3, so without this special case (mirroring the
existing one for `IFCSPACE`'s room number) the tag is never stored. Before
this chunk, no `IFCGRIDAXIS` parameter reached the BOS at all (see
`samples/snowdon-analyses/README.md`, "Converter facts").

## Type-object property sets (TKT-237)

An `IfcTypeObject` (a door type, an IFC2x3 door style, a beam type) can list
property sets in its `HasPropertySets` attribute instead of, or beside, those
an `IfcRelDefinesByProperties` attaches to each occurrence. Before TKT-237 the
converter read only the relation, so a set that lived on the type never
reached BOS. `IfcPropData` now attaches a type object's `HasPropertySets` to
the type itself, and the converter writes each value as a parameter on the
type entity: named by the property, grouped by the set, as an occurrence's own
sets are written. A member of `HasPropertySets` that is not an
`IfcPropertySet` or `IfcElementQuantity` (the Duplex door and window styles
list only an `IfcDoorLiningProperties` or `IfcWindowLiningProperties`, with
every attribute unset) adds nothing.

The values are not copied onto occurrences. An occurrence's `Entity.Type`
points at its type entity (the `Type` column of the `EntityText` view), so a
reader that wants the properties IDS 1.0 and ifctester assign to an
occurrence takes the occurrence's own parameters and adds its type's, the
occurrence's value winning for the same set and property name. The rule lives
in `Ara3D.Ifc.Conventions`' `IfcTypeObjects`.

The Fragments reader (`Ara3D.BimOpenSchema.IO.Fragments`) already wrote a
type item's `HasPropertySets` on the type entity, so this change brings the
IFC output to the same shape rather than widening a difference between them.
`tests/data/Ara3D.Ifc.Tests/ConverterTypePropertySetTests.cs` shows it on an
IFC4 door type, an IFC2x3 door style, and (when `data/model_0.ifc` is
present) every `IfcBeamType` in that model, whose `Pset_BeamCommon` is
reached only through `HasPropertySets`.
