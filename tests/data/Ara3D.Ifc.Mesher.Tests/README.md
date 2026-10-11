# Ara3D.Ifc.Mesher.Tests

Fast NUnit tests of `Ara3D.Ifc.Mesher`, the pure C# IFC mesher. Every test builds its input from a hand-written STEP snippet, so the project needs no model files and no native code, and it targets plain `net8.0` like the mesher. The whole project runs in a few seconds and is part of the default gate.

## Layout

| File | What it checks |
|---|---|
| `GoldenMeshTests.cs` | One entity type at a time: exact point and face counts, bounds, and the coverage status the mesher records |
| `BrepTests.cs`, `AdvancedFaceTests.cs`, `ThinFacetTests.cs` | Faceted and advanced breps, surface models, degenerate and sub-millimetre faces, dropped-face diagnostics |
| `CurveTests.cs`, `ProfileTests.cs`, `CompositeProfileTests.cs` | 2D curves, profile definitions, and composite profiles as steel exporters write them |
| `SweptSolidTests.cs`, `TessellatedTests.cs` | Extrusions, open-profile ribbons, triangulated and polygonal face sets |
| `BooleanTests.cs`, `CutOracleTests.cs` | Half-space and polygonal clips, checked against analytical volume and winding oracles |
| `PlacementTests.cs`, `MappedItemTransformTests.cs`, `NestedMappedItemTests.cs` | Placement chains, mapped items, mirrors, nesting |
| `ModelBuildTests.cs`, `MeshDedupTests.cs`, `InstanceColorTests.cs`, `WindingTests.cs` | Whole-model assembly from a snippet: instances, units, deduplication, colours, outward winding |
| `CoverageGateTests.cs` | Every entity the coverage table calls supported has a `[GeometryCoverage]` test in this assembly |
| `Support/MicroIfc.cs` | Wraps a snippet in an IFC4 header and parses it into a `MeshingContext`, from memory or from a temporary file |
| `Support/MeshTestAssert.cs`, `Support/GeometryOracles/` | Mesh validity, containment, winding, topology, and analytical volume checks, shared with the comparison harness |

## Running it

```powershell
dotnet test tests/data/Ara3D.Ifc.Mesher.Tests -c Release
```

Tests over real model files and the comparison against web-ifc live in `tests/data/Ara3D.IfcMeshingComparison`, which references this project for its support types.
