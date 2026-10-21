# step-corpus-harness

Meshes STEP files through the Plato library compiled to C# (`Ara3D.StepBrep`), the way `StepPlatoMesher.MeshFile` does, and times every step, edge and face. It is the corpus run's harness (`docs/plans/step-mesher-corpus-2026-10-19.md` in bim-open-toolkit) made permanent, for TKT-323's speed evaluation.

```
dotnet build tools/step-corpus-harness -c Release --artifacts-path artifacts/step-corpus-harness
step-corpus-harness time <file.step> <prefix> [--path hoisted|host] [--parallel] [--warmup]
step-corpus-harness one <file.step> <out.json> [--path hoisted|host|meshfile] [--parallel] [--warmup]
step-corpus-harness all <corpus folder> <out folder> <seconds per file> [flags as for one]
```

- `time` writes `<prefix>.edges.csv` (curve kind, the surface kinds of the faces that cite the edge, segment count, milliseconds), `<prefix>.faces.csv` (surface kind, loop and edge counts, whether every edge is a line, triangle count, the branch taken, milliseconds per stage of layer 5's `MeshRegionOnce`) and `<prefix>.summary.json` (step totals, failures, and a digest of the assembled mesh).
- `one` writes the summary only. `--path meshfile` calls `StepPlatoMesher.MeshFile` itself; its digest equals the other paths', which checks the harness's stage-by-stage restatement.
- `all` runs `one` on every `.step` and `.stp` file under the folder, each in its own process killed after the limit, and writes `all.csv` with the last stage line each file printed.
- `--path host` is `MeshFile` as committed: per edge `CanDiscretize` and `Discretize`, per face `IsDiscretizedFace`, each of which rescans the model. `--path hoisted`, the default, builds layer 4's in-range flag, edge-face table and two-edge-loop flags and layer 8's arena flag once per model, then calls `DiscretizeEdgeRangedCounted` per edge and `IsDiscretizedFaceIn` per face, as `DiscretizeAllRanged` and `FaceOutcomes` do.
- `--parallel` runs the edge and face rows with `Parallel.For`; each row still catches its own exception. `--warmup` meshes the file once first, so the timings leave out JIT compilation.

The library must be generated from the current Plato library first (`tools/regen-step-brep.ps1`). The project is not in `BimOpenData.sln`.
