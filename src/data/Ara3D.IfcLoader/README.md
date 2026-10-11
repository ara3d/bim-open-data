# Ara3D.IfcLoader

Opens an IFC file. `IfcFile(path, includeGeometry)` parses the STEP document and builds the entity model from `Ara3D.Ifc.Entities`; with `includeGeometry: true` it also loads the native web-ifc library (`web-ifc-library.dll`, Windows x64) and exposes its tessellation as `IfcModel`, `IfcMesh`, and `IfcGeometry`, which `IfcToModelConverters` turns into a `Model3D`. `IfcPropData` and `IfcRelations` read properties and relations over an open file; `IfcFileStats` counts what a file holds.

The entity model itself (`IfcEntity`, `IfcEntityResolver`, the generated `IfcTypes`) is in `Ara3D.Ifc.Entities`, portable, and keeps the `Ara3D.IfcLoader` namespace.

History: copied from `ara3d/ara3d-sdk` (`ext/Ara3D.IfcLoader`) at `82df7322`.
