# Ara3D.Ifc.Mesher

Meshes the geometry of an IFC file from its STEP definitions, in C#. It reads the same `IfcEntity` graph the rest of this repository reads (`Ara3D.IfcLoader`'s entity resolver over `Ara3D.IO.StepParser`) and needs no native code: the web-ifc DLL that `IfcFile(path, includeGeometry: true)` loads is never touched, so a file opened for its data can be meshed without reopening it.

```csharp
using var file = new IfcFile(path, includeGeometry: false);
IfcMeshingResult result = IfcMesher.Build(file.EntityResolver);   // or IfcMesher.Build(path)
```

`IfcMeshingResult` holds a `Model3D` (meshes, instances with transforms and materials, each instance keyed to the product's express id), the counts and bounds, and `Messages`: one line per IFC entity kind the mesher approximated or could not handle, and one line per product that lost faces. `Success` is false when the whole build threw; a product that fails is recorded and skipped.

The IFC MCP server's `ifc_mesh`, `ifc_bounds`, `ifc_volume` and `ifc_meshing_diagnostics` tools report this mesher's output, and the toolkit's `BimOpenFlow.Nodes.Geometry` meshes the model the 3D pane draws from it.

## Layout

One pipeline, read top to bottom:

| File | Does |
|---|---|
| `IfcMesher.cs` | Entry point; wraps a build in an `IfcMeshingResult` |
| `ModelAssembler.cs` | Walks the products, collects their parts, carves openings, dedups meshes into a `Model3D` |
| `GeometryPartCollector.cs` | Representation items of one product to placed parts, following mapped items |
| `GeometryDispatcher.cs` | One IFC geometry entity to one builder below, by entity name |
| `SweptSolids.cs` | Extrusions, revolutions, swept disks, ribbons |
| `ProfileBuilder.cs` | Profile definitions (rectangles, circles, I and L shapes, arbitrary and composite) to a `PolygonWithHoles` |
| `CurveEvaluator.cs` | Curves (polylines, conics, trimmed, composite, B-spline control polygons) to point lists |
| `Brep.cs` | Faceted and advanced breps, face sets and shells, with planar, cylindrical, revolved and B-spline face surfaces |
| `Tessellated.cs` | Triangulated and polygonal face sets |
| `Booleans.cs` | Half-space clipping, including polygonal-bounded half-spaces |
| `OpeningCarver.cs` | `IfcRelVoidsElement` openings carved from convex hosts, with reveal caps |
| `Placements.cs` | Local placements, axis placements, and Cartesian transformation operators to frames and matrices |
| `Units.cs` | The project's length unit to metres |
| `StyleResolver.cs` | `IfcStyledItem` to a `Material` |
| `PolygonWithHoles.cs` | A profile ring with holes; triangulation goes through `Ara3D.Geometry.Earcut` |
| `MeshingContext.cs` | Per-build state: resolver, unit scale, caches, diagnostics |
| `Diagnostics.cs` | Entity support status and dropped-face records |
| `GeometryCoverage.cs` | The table of geometry entities handled, how well, and the fixture that proves each |
| `Tolerance.cs` | The one absolute tolerance, 1e-6 m |
| `IfcMeshingResult.cs`, `IfcModelStats.cs` | The result type and the counts derived from a model |

## How it is tested and measured

Tests and the comparison against web-ifc live in `tests/data/Ara3D.IfcMeshingComparison`: micro-IFC fixtures that assert exact meshes for one entity at a time, corpus tests over the model files in `data/`, and a parity scorecard against web-ifc's tessellation of the same files. The toolkit's tickets hold the measured gaps: `TKT-188` (walls under roofs are not clipped) and `TKT-190` (curves are sampled about 1.45 times more densely than web-ifc).

## History

Copied from `ara3d/ara3d-sdk` at `82df7322` (`wip/Ara3D.Ifc.Mesher`) on 2026-08-30 and developed here since; the SDK no longer has it.
