# IFC mesher parity results

## Run of 2026-10-11

Mesher at bim-open-data commit `6f3a29c` (the commit before this file was added) (after the layout work under toolkit ticket TKT-216), web-ifc as shipped by `Ara3D.IfcLoader`, model files from `ara3d/IFC-toolkit` at `9ad378a`. How to read the metrics is in [README.md](README.md).

| File | Parity | Meshes | Instances | Entity bbox | Entity shape | Mesh shape | Triangles (mesher / web-ifc) |
|---|---:|---:|---:|---:|---:|---:|---:|
| IfcOpenHouse_IFC4.ifc | 0.963 | 19 / 19 | 38 / 38 | 35/35 | 32/35 | 0.955 | 1,175 / 1,098 |
| example.ifc | 0.938 | 99 / 99 | 120 / 120 | 115/116 | 88/116 | 0.900 | 12,494 / 15,046 |
| steelplates.ifc | 0.918 | 14 / 14 | 14 / 14 | 14/14 | 10/14 | 0.727 | 716 / 1,428 |

All five `IfcMesherParity` gates pass. Every product that web-ifc meshes, the mesher also meshes (instance Jaccard 1.000 on all three files), and the mesh and instance counts agree exactly.

**What the shortfalls are.** The scores below 1 are shape and density, not coverage:

- On `example.ifc`, one product's bounds and 28 of 116 products' shapes differ from web-ifc. The comparison test `Example_SharedEntityBoundingBoxesMatchOracle` fails on that one box (115 of 116). Which product it is, and whether the mesher or web-ifc is wrong, is not established; the diagnostic `DiagnoseExampleGaps` lists the candidates.
- On `steelplates.ifc`, the mesher emits half as many triangles as web-ifc (716 against 1,428) and 4 of 14 shapes differ; the clipped beams are the likely source, since the file has little else, but this has not been confirmed by inspecting them.
- On `IfcOpenHouse_IFC4.ifc`, the mesher emits 7 % more triangles than web-ifc.

Two measured gaps on other files are tracked in the toolkit: TKT-188 (upper walls on AC20-FZK-Haus not clipped by their roofs, measured per element against the BOS geometry on 2026-10-10) and TKT-190 (FM_ARC_DigitalHub meshes to 1.45 times web-ifc's triangle count; the cause is not yet measured). Neither file is in this set. The mesher's July 2026 development notes were deleted in the layout work and nothing here is taken from them.
