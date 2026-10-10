# Ara3D.BimOpenSchema.IO.Gltf

Writes the geometry of a BIM Open Schema (BOS) model as a binary glTF (`.glb`) file, with no
IFC loader in the way. Every glTF node is one BOS instance and carries, in its `extras`, the
entity it draws, so a viewer's pick leads back to the element. The first node of
`samples/public/duplex.bos`:

```json
{ "extras": { "entityIndex": 2561, "globalId": "0wkEuT1wr1kOyafLY4vyu$" },
  "name": "M_Tall Cabinet-Single Door(2):800 mm:800 mm:157200",
  "matrix": [1,0,0,0, 0,0,-1,0, 0,1,0,0, 2.914,4.1,11.4067,1],
  "mesh": 0 }
```

three.js's `GLTFLoader` copies a node's `extras` into the object's `userData`.

`entityIndex` is the row in the BOS `Entities` table; `globalId` is that entity's GlobalId (the
IFC GlobalId or Revit UniqueId). An id the model does not have is left out, not filled in. The
keys are `BosGlb.EntityIndexKey` and `BosGlb.GlobalIdKey`.

## Calling it

```csharp
using Ara3D.BimOpenSchema.IO.Gltf;

// From a file
GlbExportSummary s = BosGlb.WriteGlb("model.bos", "model.glb");

// From a loaded model, limited to some entities, hidden instances included
s = bimData.WriteGlb("doors.glb", new GlbExportOptions { EntityIndices = doorEntities, IncludeHidden = true });

// In memory, to write another container or add to the scene
var (model, summary) = bimData.ToGltf();   // SharpGLTF.Schema2.ModelRoot
```

The summary counts nodes, glTF meshes, materials, triangles drawn, instances skipped because
their mesh is empty, and the bytes written. `UnmatchedEntityIndices` counts the requested
entity indices that produced no node (out of range, no geometry, or only hidden instances): a
caller that passed STEP ids or Revit element ids instead of entity indices sees it equal the
number it passed, and a file with no nodes.

## What the file holds

- **Axes and units.** BOS is z-up in metres; glTF is y-up in metres. Each node's matrix is the
  BOS transform followed by a rotation that maps (x, y, z) to (x, z, -y). Vertex positions stay
  as BOS stores them (integers over 10,000, written as float metres) in each mesh's local space.
- **Meshes.** Each BOS mesh in use is written once, positions in one buffer view and uint32
  indices in another. glTF binds a material to a mesh, not to a node, so each pair of BOS mesh
  and BOS material in use becomes a glTF mesh over the same two accessors.
- **Materials.** One PBR metallic-roughness material per BOS material: base colour, alpha
  (blended below 1), metallic, roughness, always double-sided. An instance with no material
  gets the SDK's default grey.
- **Hidden instances** (`InstanceFlags` bit `IsHidden`, set by the converter that wrote the
  `.bos`) are left out unless `IncludeHidden` is set.
- **Bad indices.** Every index in the Instances table is checked against the table it points
  into. A mesh index out of range or an empty mesh skips the instance (`SkippedEmpty`); a
  transform index out of range skips it (`SkippedBadTransform`); a material index out of range
  draws it with the default grey (`DefaultedMaterials`). An `InstanceFlags` column shorter than
  the Instances table (older files, hand-built geometry) leaves the missing instances visible.
  The mesh offsets into the vertex and index buffers are trusted.

## What it does not do

- No normals, UVs, or textures: glTF viewers shade flat when normals are missing.
- No hierarchy: nodes are flat under the scene root, not grouped by storey or by entity.
- No compression or quantisation: positions are 32-bit floats, so a `.glb` is 8 to 14 times the size of
  its `.bos` on the public samples (BOS stores Parquet-compressed integers).
- No GPU instancing (`EXT_mesh_gpu_instancing`); a shared mesh is shared by reference only.
- It reads the whole `.bos`, parameters included, through `ParquetUtils.ReadBimDataFromParquetZip`.

## Measured

Release build, one warm process, on the owner's Windows machine (2026-10-10), from
`PublicSampleTests.Measure_public_samples` in the test project. Triangles are drawn triangles (an instanced mesh
counts once per node). The Khronos glTF validator 2.0.0-dev.3.10 reports no errors, warnings,
infos, or hints on any of these files.

| Sample | `.bos` bytes | Instances | Nodes | Meshes | Triangles | `.glb` bytes | Read ms | Write ms |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `duplex.bos` | 97,870 | 681 | 660 | 414 | 26,774 | 930,520 | 22 | 18 |
| `schependomlaan.bos` | 1,113,111 | 5,978 | 5,972 | 4,696 | 261,503 | 8,274,220 | 288 | 147 |
| `digitalhub-federated.bos` | 4,853,636 | 14,293 | 14,229 | 7,366 | 2,588,151 | 47,755,620 | 1,103 | 985 |

On `duplex.bos` the 21 instances not written are hidden. Tests are in
[`tests/data/Ara3D.BimOpenSchema.IO.Gltf.Tests`](../../../tests/data/Ara3D.BimOpenSchema.IO.Gltf.Tests).

## Validating the output

The test project's `validator/` folder pins the Khronos glTF validator (npm
`gltf-validator` 2.0.0-dev.3.10) and holds `validate-gltf.mjs`, which validates .glb files or
every .glb in a folder and exits 1 when any file has an error:

```
cd tests/data/Ara3D.BimOpenSchema.IO.Gltf.Tests/validator
npm ci
node validate-gltf.mjs path/to/model.glb path/to/folder
```

To write every public sample and validate them all, run the explicit test (it is ignored when
node is not on PATH or `npm ci` has not run):

```
dotnet test tests/data/Ara3D.BimOpenSchema.IO.Gltf.Tests -c Release --filter "FullyQualifiedName~Public_samples_pass_the_Khronos_validator"
```

## Dependencies

[Ara3D.BimOpenSchema.IO](../Ara3D.BimOpenSchema.IO) to read `.bos`, and `Ara3D.IO.SharpGLTF`
(the SDK's copy of SharpGLTF.Core) to build and write the glTF.
