# IFC mesher parity results

## Five meshers, run of 2026-10-11

The same three files meshed by five tools, from the committed dumps under `dumps/` and the two in-process backends (`meshers.json` has the numbers; `MesherMatrixTests` produced them; bim-open-data at the commit that added this section). Time is one run on the owner's machine: the in-process meshers include the file parse, the external ones only the meshing call. Parity is `ModelComparer`'s weighted mean with the column mesher as the oracle, so a row reads "how close this mesher is to that one"; the metrics are not symmetric and the two orders differ slightly.

### IfcOpenHouse_IFC4.ifc

| Mesher | Version | Meshes | Instances | Triangles | Time (ms) |
|---|---|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | in process | 19 | 38 | 1,175 | 371 |
| WebIfcDll | in process | 19 | 38 | 1,098 | 13 |
| WebIfc | 0.0.78 | 18 | 38 | 1,098 | 53 |
| IfcLite | 4.15.0 | 39 | 39 | 1,172 | 23 |
| IfcOpenShell | 0.8.5 | 39 | 39 | 1,146 | 224 |

| Candidate \ Oracle | Ara3D.Ifc.Mesher | WebIfcDll | WebIfc | IfcLite | IfcOpenShell |
|---|---:|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | – | 0.963 | 0.960 | 0.924 | 0.922 |
| WebIfcDll | 0.963 | – | 0.996 | 0.901 | 0.905 |
| WebIfc | 0.960 | 0.996 | – | 0.899 | 0.902 |
| IfcLite | 0.924 | 0.901 | 0.899 | – | 0.997 |
| IfcOpenShell | 0.922 | 0.905 | 0.902 | 0.997 | – |

### example.ifc

| Mesher | Version | Meshes | Instances | Triangles | Time (ms) |
|---|---|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | in process | 99 | 120 | 12,494 | 78 |
| WebIfcDll | in process | 99 | 120 | 15,046 | 19 |
| WebIfc | 0.0.78 | 98 | 119 | 14,660 | 67 |
| IfcLite | 4.15.0 | 116 | 116 | 12,016 | 15 |
| IfcOpenShell | 0.8.5 | 116 | 116 | 10,508 | 167 |

| Candidate \ Oracle | Ara3D.Ifc.Mesher | WebIfcDll | WebIfc | IfcLite | IfcOpenShell |
|---|---:|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | – | 0.938 | 0.937 | 0.950 | 0.943 |
| WebIfcDll | 0.938 | – | 0.988 | 0.929 | 0.923 |
| WebIfc | 0.937 | 0.988 | – | 0.930 | 0.925 |
| IfcLite | 0.950 | 0.929 | 0.930 | – | 0.976 |
| IfcOpenShell | 0.943 | 0.923 | 0.925 | 0.976 | – |

### steelplates.ifc

| Mesher | Version | Meshes | Instances | Triangles | Time (ms) |
|---|---|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | in process | 14 | 14 | 716 | 11 |
| WebIfcDll | in process | 14 | 14 | 1,428 | 29 |
| WebIfc | 0.0.78 | 14 | 14 | 1,428 | 68 |
| IfcLite | 4.15.0 | 16 | 16 | 2,528 | 22 |
| IfcOpenShell | 0.8.5 | 16 | 16 | 1,340 | 164 |

| Candidate \ Oracle | Ara3D.Ifc.Mesher | WebIfcDll | WebIfc | IfcLite | IfcOpenShell |
|---|---:|---:|---:|---:|---:|
| Ara3D.Ifc.Mesher | – | 0.918 | 0.918 | 0.882 | 0.904 |
| WebIfcDll | 0.918 | – | 0.997 | 0.900 | 0.909 |
| WebIfc | 0.918 | 0.997 | – | 0.902 | 0.912 |
| IfcLite | 0.882 | 0.900 | 0.902 | – | 0.964 |
| IfcOpenShell | 0.904 | 0.909 | 0.912 | 0.964 | – |

**What the matrix shows.** The five fall into two families. The two web-ifc builds agree with each other at 0.988 to 0.997, and IFClite agrees with IfcOpenShell at 0.964 to 0.997 (IFClite's README says it is checked element by element against IfcOpenShell). Across the families the scores are 0.88 to 0.95. The pure C# mesher scores 0.92 to 0.96 against both families, and it never scores lower against one family than the families score against each other, so on these files it is not an outlier. Three differences are visible in the counts, not yet explained:

- IFClite and IfcOpenShell emit one more instance than the web-ifc builds and the C# mesher on `IfcOpenHouse_IFC4.ifc` (39 against 38) and two more on `steelplates.ifc` (16 against 14), and one mesh per product (no sharing of repeated geometry).
- Triangle counts differ by up to a factor of 3.5 on `steelplates.ifc` (716 for the C# mesher, 2,528 for IFClite); the files hold clipped beams and plates, where each mesher's booleans tessellate differently.
- web-ifc 0.0.78 drops one instance of `example.ifc` (119 against 120) that the older DLL build still meshes.

The web-ifc dumps were first written in web-ifc's Y-up frame (bounds with Y and Z swapped, parity 0.76 to 0.89 against everything); the adapter now rotates each placement back to IFC's Z-up frame, and the numbers above are from the corrected dumps.

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
