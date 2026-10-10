# Ara3D.BimOpenSchema.IO.Usd

Writes a BIM Open Schema (BOS) model as an OpenUSD text stage (`.usda`) that opens in
Omniverse, Blender, Houdini, usdview, and anything else that reads USD. Element data comes
along: each element's GlobalId, name, category, type, and parameters are typed `bim:`
attributes on its prim.

There is no native USD dependency. The writer streams text to a `TextWriter`, so memory
follows the size of the BOS model, never the size of the output.

## Calling it

```csharp
using Ara3D.BimOpenSchema.IO;      // ReadBimDataFromParquetZip
using Ara3D.BimOpenSchema.IO.Usd;  // WriteUsda

var data = new FilePath("samples/public/duplex.bos").ReadBimDataFromParquetZip();
UsdExportSummary summary = data.WriteUsda("duplex.usda");
```

`WriteUsda(this IBimData, string path)` replaces the file; `WriteUsda(this IBimData, TextWriter)`
writes to a writer the caller owns. Both return a `UsdExportSummary`: counts of materials,
prototypes, declared parameter attributes, elements, instances, and of what was left out
(instances with no mesh, parameters with no value, duplicate parameters).

The project reads only `IBimData` (from `Ara3D.BimOpenSchema`) and the geometry helpers in
`Ara3D.BimOpenSchema.ObjectModel`; reading a `.bos` file is the caller's job.

## What the stage holds

Stage metadata: `upAxis = "Z"`, `metersPerUnit = 1`, `defaultPrim = "Model"`. BOS is already
z-up in metres, so nothing is converted.

```
/Model                         Xform, kind = assembly
  /Materials/M{i}              one UsdPreviewSurface material per BOS material row
  /Prototypes/P{i}/Mesh        one mesh per BOS mesh row, under an abstract ("class") scope
  /E_{GlobalId}                one Xform per entity with geometry, kind = component
      bim:... attributes
      /I{k}                    BOS instance k: instanceable, references /Model/Prototypes/P{mesh}
  /Unassigned/I{k}             instances whose entity index is missing (only when there are some)
  /Descriptors                 each parameter attribute declared once, with its BOS strings
```

- **Instancing.** Each BOS instance is a prim marked `instanceable = true` that references its
  mesh's prototype, so a mesh used by many instances is stored once. USD shares an instance's
  *descendants*, not the instance prim, which is why the mesh sits one level below `P{i}`.
  Instanceable references were chosen over a `PointInstancer` because a point instancer
  collapses every instance into one prim: elements could no longer be selected, hidden, or
  inspected one by one, and per-element attributes would have nowhere to live.
- **Transforms.** BOS composes scale, then rotation, then translation, which is USD's
  `["xformOp:translate", "xformOp:orient", "xformOp:scale"]`. The quaternion is written
  real part first, `(w, x, y, z)`, as USD expects. Ops equal to the identity are left out.
- **Materials.** Bound per instance with `material:binding` on the instance prim (with
  `MaterialBindingAPI` applied); an instance with no BOS material has no binding. Colours are
  the BOS bytes divided by 255. BOS does not say whether its colours are sRGB or linear, so
  they are written unchanged.
- **Meshes.** Triangles, `doubleSided = 1` (BIM meshes are not reliably closed or consistently
  wound), `subdivisionScheme = "none"`, an `extent`, and points in metres written as exact
  decimals of BOS's 0.1 mm integers. No normals are written; renderers compute them, and BOS
  meshes are mostly unwelded per face (Schependomlaan has 1.7 triangle corners per vertex).
- **Hidden instances** (BOS flag `IsHidden`) get `visibility = "invisible"`.

### Element prim names

`E_` followed by the GlobalId, with each character outside `[A-Za-z0-9_]` replaced by `_`
(IFC GlobalIds use `$`). When the GlobalId is missing, or a second entity would get the same
name (federated models can repeat GlobalIds), the prim is `E{entity index}` instead. The real
GlobalId is always in `bim:globalId`.

### Attributes on an element prim

| Attribute | USD type | Written when |
|---|---|---|
| `bim:entityIndex` | `int` | always: the BOS entity row |
| `bim:localId` | `int64` | the local id is not negative (-1 marks "none"): STEP line number (IFC) or ElementId (Revit) |
| `bim:globalId` | `string` | the GlobalId is present and not empty |
| `bim:name` | `string` | the name is present and not empty |
| `bim:category` | `string` | the entity has a category whose name is not empty |
| `bim:type` | `string` | the entity has a type whose name is not empty |
| `bim:document` | `string` | the entity's document has a title |
| `bim:param:{group}:{name}` | by parameter type | the entity has a value for that descriptor |

Parameter types map as Int to `int`, Number to `float` (BOS numbers are 32-bit), String to
`string`, Point to `point3f`, and Entity to `string` holding the referenced entity's name (the
target, often a storey or a type, usually has no prim of its own).

A missing value is left out, never defaulted: a Number whose index is -1, a String index past
the end of the string table, an Entity reference whose target has no name. A value BOS stores
as an empty string is written as `""`. The summary counts what was left out.

`{group}` and `{name}` are the descriptor's group and name made into identifiers the same way
as prim names (`Ifc:OverallWidth` becomes `Ifc_OverallWidth`; a leading digit gets a `_`).
When two descriptors would get one attribute name, the later one gets `_{descriptor index}`
appended. Element prims carry values only; the exact BOS strings are on the declaration of the
same attribute under `/Model/Descriptors`:

```
def Scope "Descriptors"
{
    custom float bim:param:IFCDOOR:Ifc_OverallWidth (
        customData = {
            string bosType = "Number"
        }
        displayGroup = "IFCDOOR"
        displayName = "Ifc:OverallWidth"
    )
}
```

`customData` also holds `string units` when the descriptor has units. Writing these strings
on every element instead made Schependomlaan 32% larger (56.1 MB against 42.7 MB).

## Measured

Written on 2026-10-10 on the owner's Windows 11 machine, .NET 8, Release build, timing the
`WriteUsda` call alone (the BOS read is not included), first call in the process; numbers come
from the output of `SampleExportTests` (three runs for Schependomlaan). The `.usdc` and `.usdz`
sizes come from converting with usd-core 0.26.8 as shown below.

| Model | BOS | Instances | Meshes | Elements | `bim:` attributes | `.usda` | Write time | `.usdc` | `.usdz` |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `duplex` | 0.1 MB | 681 | 435 | 236 | 14,602 | 3.0 MB | 44 ms | 0.47 MB | 0.47 MB |
| `schependomlaan` | 1.1 MB | 5,978 | 4,702 | 3,510 | 312,470 | 42.7 MB | 280 to 310 ms | 4.7 MB | 4.7 MB |

In the Schependomlaan text, parameter values are 57% of the bytes, mesh points 17%, triangle
indices 6%, and the Descriptors scope 4.5%. usd-core 0.26.8 parses the `.usda` in 1.3 s and opens the `.usdc` in 0.8 s. Gzip takes the
`.usda` to 3.2 MB.

Text is about nine times the size of USD's binary crate format here. Scaled linearly to
Snowdon's roughly 450,000 instances (75 times Schependomlaan) that would be about 3 GB of
text and some 20 seconds of writing, against about 350 MB as `.usdc`; that is an
extrapolation, not a measurement. A direct `.usdc` writer is the next step if models of that
size need USD often. Until then, convert after writing.

## Converting to .usdc or .usdz

With usd-core (`python -m pip install usd-core`; its wheel has no command-line tools):

```python
from pxr import Sdf, UsdUtils
Sdf.Layer.FindOrOpen("duplex.usda").Export("duplex.usdc")      # binary crate
UsdUtils.CreateNewUsdzPackage("duplex.usdc", "duplex.usdz")     # one zip-packed file, e.g. for Apple AR tools
```

With a full USD build, `usdcat duplex.usda -o duplex.usdc` and `usdzip duplex.usdz duplex.usdc`
do the same.

## What it does not do

- Read USD. USD rarely carries BIM data, so input is out of scope.
- Write `.usdc` or `.usdz` itself (see above).
- Write relations (containment, hosting, storeys) other than as Entity parameters, or entities
  without geometry (types, storeys, property sets) as prims.
- Write normals, UVs, textures, or per-vertex colours; BOS has none of the last three.
- Convert colours between sRGB and linear.
- Merge several BOS models into one stage.
