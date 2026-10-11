# Ara3D.Ifc.Entities

The IFC entity model over a STEP document, with no native code: `net8.0`, so a reader of IFC content that needs no geometry from web-ifc can build without Windows. `Ara3D.IfcLoader` adds `IfcFile` and the web-ifc geometry on top of it; `Ara3D.Ifc.Mesher` builds on this project alone.

The types keep the `Ara3D.IfcLoader` namespace they had when they lived there, so their users compile unchanged.

| File | Holds |
|---|---|
| `IfcEntity.cs` | One STEP entity: id, definition, attribute tokens, typed readers, and its name by `Ara3D.Ifc.Conventions.IfcNames` |
| `IfcEntityResolver.cs` | Every entity of a `StepDocument` by express id |
| `IfcSchemas.cs` | Which schema the header names: IFC4X3, IFC4, else IFC2X3 |
| `IfcStringDecoder.cs` | STEP string escapes (`\X2\`, `\S\`, ...) to text |
| `IfcInstanceTypeRelations.cs` | Occurrence to type relations |
| `../Ara3D.IfcTypes` (shared project) | The generated entity, enum, and attribute tables for the three schemas |
