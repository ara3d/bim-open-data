# step-corpus-harness

Meshes STEP files through the Plato library compiled to C# (`Ara3D.StepBrep`), the way `StepPlatoMesher.MeshFile` does, and times every step, edge and face. It is the corpus run's harness (`docs/plans/step-mesher-corpus-2026-10-19.md` in bim-open-toolkit) made permanent, for TKT-323's speed evaluation.

```
dotnet build tools/step-corpus-harness -c Release --artifacts-path artifacts/step-corpus-harness
step-corpus-harness time <file.step> <prefix> [--path host] [--parallel] [--warmup]
step-corpus-harness one <file.step> <out.json> [--path host|meshfile] [--parallel] [--warmup]
step-corpus-harness all <corpus folder> <out folder> <seconds per file> [flags as for one]
step-corpus-harness gallery <file.step | witness:Name[:x]> <out.glb> [--faces a,b,c] [--ring n] [--chordal x] [--parallel] [--warmup]
python tools/step-corpus-harness/gallery-page.py <folder of .glb and .json> [index.html]
```

- `time` writes `<prefix>.edges.csv` (curve kind, the surface kinds of the faces that cite the edge, segment count, milliseconds), `<prefix>.faces.csv` (surface kind, loop and edge counts, whether every edge is a line, triangle count, the branch taken, milliseconds per stage of layer 5's `MeshRegionOnce`) and `<prefix>.summary.json` (step totals, failures, and a digest of the assembled mesh).
- `one` writes the summary only. `--path meshfile` calls `StepPlatoMesher.MeshFile` itself; its digest equals the other paths', which checks the harness's stage-by-stage restatement.
- `all` runs `time` on every `.step` and `.stp` file under the folder, each in its own process killed after the limit, so the out folder holds every file's edge and face tables and summary (the per-surface-kind counts of a corpus report come from the face tables), and writes `all.csv` with the last stage line each file printed.
- `gallery` writes one model as a GLB for the gallery page (`docs/plans/step-mesher-gallery.md` in bim-open-toolkit): every meshed face as triangles coloured by surface kind (quads split), every discretized edge as a line, and the edges of every unmeshed face as red lines, with `<out>.json` beside it (counts, time per stage, the check, the slowest and most-edged faces, every unmeshed face with its branch and message). `witness:Name` meshes the library's `Witness_Name()` model, `witness:Name:0.3` the overload taking one Number; `--faces` with `--ring` keeps a close-up of named faces and their neighbours; `--chordal` replaces the host's chordal bound (a witness is one unit across, so 0.01). `gallery-page.py` turns a folder of those into the page, its cases listed in the script.
- `--path host`, the default, is the host's own path timed per row: `StepPlatoTables.Of` (layer 4's in-range flag, edge-face table and two-edge-loop flags and layer 8's arena flag, once per model, TKT-402), then the host's `Discretized` per edge and `IsDiscretized` and `MeshFace` per face. The summary carries a `MeshCheck` (boundary, non-manifold and misoriented edge counts of the assembled mesh) beside the digest.
- `--parallel` runs the edge and face rows with `Parallel.For`, as `MeshFile` does since TKT-403; without it the rows run in index order, which is the sequential witness for the host's parallel digest. Each row still catches its own exception. `--warmup` meshes the file once first, so the timings leave out JIT compilation.

The library must be generated from the current Plato library first (`tools/regen-step-brep.ps1`). The project is not in `BimOpenData.sln`.
