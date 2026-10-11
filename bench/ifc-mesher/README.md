# IFC mesher parity benchmark

How close `Ara3D.Ifc.Mesher`, the pure C# IFC mesher in `src/data/Ara3D.Ifc.Mesher`, comes to web-ifc's tessellation of the same files. web-ifc is the reference because the toolkit's IFC loader already ships it; the comparison treats it as an oracle, not as ground truth, and the metrics are built so that a web-ifc artefact (a mesh tagged with the wrong entity, an open shell) does not count against the mesher. Results are in [RESULTS.md](RESULTS.md); the machine-readable scorecard is `scorecard.json`.

## What is measured

The quick comparison set is three files from the [ara3d/IFC-toolkit](https://github.com/ara3d/IFC-toolkit) `test-files` folder: `IfcOpenHouse_IFC4.ifc` (IfcOpenShell's sample house), `example.ifc` (a steel-framed structure), and `steelplates.ifc` (plates and clipped beams). For each file the harness builds the model with both meshers and scores:

| Metric | What it compares |
|---|---|
| Mesh count, instance count | The number of distinct meshes and placed instances |
| Entity instances | Which IFC products got geometry (Jaccard over product ids) |
| Entity bbox | Per product, the world bounds of its geometry within 5 % |
| Entity shape | Per product, volume, oriented box, principal axes, and boundary, rotation invariant; products whose oracle mesh matches a different product better are excluded as mis-tags |
| Mesh bbox, mesh shape | The same per distinct mesh, paired by fingerprint |
| Merged mesh | The whole model as one mesh: the mean of point-count similarity, triangle-count similarity, and bounds closeness |

**Parity** is the weighted mean of these; the weights are `ModelComparerOptions.DefaultWeights` in `Harness/ModelComparer.cs`. A score of 1 means the two meshers agree on every metric, not that either is right.

## Running it

The model files are not committed. Clone `ara3d/IFC-toolkit` and point the harness at its `test-files` folder:

```powershell
$env:ARA3D_IFC_TEST_DIRS = 'C:\path\to\IFC-toolkit\test-files'
dotnet build tests/data/Ara3D.IfcMeshingComparison -c Release
dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release --no-build --filter "FullyQualifiedName~ScoreQuickComparisonFiles"
dotnet test tests/data/Ara3D.IfcMeshingComparison -c Release --no-build --filter "Category=IfcMesherParity"
```

The first test rewrites `scorecard.json` here; the web-ifc tessellations it needs are generated once into `artifacts/ifc-mesher/oracles/` and reused. The second runs the regression gates in `Tests/Comparison/ParityGates.cs`, which read the same comparison: parity at least 0.85 and instance Jaccard at least 0.99 on each quick file, round-trip of entity assignment through the BFAST cache, and the duplex placement control. After a change to the mesher, run both, read the diff of `scorecard.json`, and add a dated section to `RESULTS.md` when a number moves.

`scorecard.json` carries the run's timestamp, so a rerun that changes no score still changes the file; commit it only when a score moved.

## Where the tests are

`tests/data/Ara3D.IfcMeshingComparison/Tests/Comparison/ScorecardTests.cs` writes the scorecard; `Harness/ModelComparer.cs` holds the metrics. The per-model gates with tighter thresholds (the AISC sculpture at 0.92, the Institute models at 0.85 with a triangle ratio floor) are under `Tests/Corpus/` and run by name.
