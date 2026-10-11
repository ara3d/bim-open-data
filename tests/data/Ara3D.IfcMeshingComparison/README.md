# Ara3D.IfcMeshingComparison

Tests of `Ara3D.Ifc.Mesher`, the pure C# IFC mesher, and its comparison against web-ifc's tessellation of the same files. NUnit drives both; the comparison runs are `[Explicit]`, chosen by name or category.

## Backends

| Name | Source | Role |
|---|---|---|
| `WebIfcDll` | `IfcFile(includeGeometry: true).ToModel3D()` | The native web-ifc tessellation, used as the oracle |
| `Ara3D.Ifc.Mesher` | `ModelAssembler.BuildModel(file.EntityResolver)` | The mesher under test |

`Tests/Native/IfcMeshingTests` checks the web-ifc path itself (geometry enumeration, buffers, `ToModel3D`).

## Layout

```
Tests/
  Native/        web-ifc checks
  PureCSharp/    the mesher: micro-IFC fixtures that assert exact meshes, and corpus tests over model files
  Comparison/    backend smoke tests, the parity scorecard, and the per-model comparison runs
  Support/       MicroIfc (a STEP snippet to a MeshingContext), MeshTestAssert, GeometryCoverageAttribute
Harness/         TestFiles (where models are and outputs go), the web-ifc BFAST oracle, ModelComparer and its metrics
Meshers/         IMeshingBackend and the two backends
Reporting/       Markdown and JSON reports of a comparison run
```

## Model files

`Harness/TestFiles.cs` looks for a model by file name in the repository's `data/` (filled by `data/get-test-data.ps1`) and then in each folder named by the `ARA3D_IFC_TEST_DIRS` environment variable (separated by `;`). A test whose file is absent is ignored, not failed, so the default gate passes with no models at all. The quick comparison set is `IfcOpenHouse_IFC4.ifc`, `example.ifc`, and `steelplates.ifc`.

## Outputs

Everything the harness writes goes under `artifacts/ifc-mesher/`, which git ignores: `oracles/` (web-ifc BFAST tessellations, generated once per model and reused), `reports/` (comparison and coverage reports, oracle maps), and `probes/` (meshes a test exports, named after the model). Nothing is written beside a model file.

## Running it

```powershell
dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release                              # the gate: fast tests, no models needed
$env:ARA3D_IFC_TEST_DIRS = 'C:\models'; dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release   # with a corpus
dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release --filter "Category=IfcMesherParity"       # the parity scorecard
dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release --filter "Name~RunFullComparison"         # every model against web-ifc, with reports
```
