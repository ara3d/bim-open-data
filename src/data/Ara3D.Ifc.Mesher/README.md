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
| `ModelAssembler.cs`, `MeshDedup.cs`, `AggregatedVoidHosts.cs` | Walks the products, collects their parts, carves openings, dedups meshes into a `Model3D`; void hosts without their own representation get the carved union of their aggregated children |
| `GeometryPartCollector.cs` | Representation items of one product to placed parts, following mapped items |
| `GeometryDispatcher.cs` | One IFC geometry entity to one builder below, by entity name |
| `SweptSolids.cs`, `Revolutions.cs`, `SweptDisks.cs`, `DirectrixSweeps.cs`, `SweepRings.cs` | Extrusions and ribbons, revolutions, swept disks, sweeps along a directrix |
| `ProfileBuilder.cs`, `BasicProfiles.cs`, `SteelProfiles.cs`, `ProfilePoints.cs` | Profile definitions to a `PolygonWithHoles`: arbitrary and composite curves, rectangles and circles, the steel sections |
| `CurveEvaluator.cs` | Curves to point lists: dispatch by entity name, and ring cleaning |
| `PolyCurves.cs`, `Conics.cs`, `TrimmedCurves.cs`, `CompositeCurves.cs`, `BSplineCurves.cs` | One curve family each: polylines and indexed polycurves; circles, ellipses and lines; trimmed curves; composite curves and their joins; B-spline control polygons |
| `Brep.cs` | Faceted and advanced breps, face-based and shell-based surface models, single faces: each a set of faces |
| `FaceSetBuilder.cs` | Triangulates a set of faces into one welded mesh, dropping and recording faces it cannot |
| `FaceBounds.cs` | A face's bounds (polyloops, edge loops, curve-bounded planes) to 3D rings |
| `SurfaceMap.cs`, `PlanarMap.cs`, `CylinderMap.cs`, `RevolutionMap.cs`, `BSplineSurfaceMap.cs` | Projection of a face ring to 2D for triangulation and back onto its surface: a plane, a cylinder, a surface of revolution, or a B-spline surface |
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

`tests/data/Ara3D.Ifc.Mesher.Tests` holds the fast tests: micro-IFC fixtures that assert exact meshes for one entity at a time, with no model files. `tests/data/Ara3D.IfcMeshingComparison` holds the corpus tests over the model files in `data/` and a parity scorecard against web-ifc's tessellation of the same files. The committed parity numbers are in [`bench/ifc-mesher/`](../../../bench/ifc-mesher/README.md). The toolkit's tickets hold the open gaps: `TKT-65` (shape and density differences on the quick files), `TKT-188` (upper walls on AC20-FZK-Haus are not clipped by their roofs), and `TKT-190` (FM_ARC_DigitalHub meshes to 1.45 times web-ifc's triangle count; cause not yet measured).

## History

Copied from `ara3d/ara3d-sdk` at `82df7322` (`wip/Ara3D.Ifc.Mesher`) on 2026-08-30 and developed here since; the SDK no longer has it.
