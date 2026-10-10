# Ara3D.BimOpenSchema.IO.Usd

Writes a BIM Open Schema (BOS) model as an OpenUSD text stage (`.usda`) that opens in
Omniverse, Blender, Houdini, usdview, and anything else that reads USD. Every BOS entity
becomes a prim, with or without geometry. Its GlobalId, name, category, type, and parameters
are typed `bim:` properties on the prim, and its relations (the storey it is in, the host it
sits in) are `bim:` relationships to other entity prims.

There is no native USD dependency. The writer streams text to a `TextWriter`, so memory
follows the size of the BOS model, never the size of the output.

## Calling it

```csharp
using Ara3D.BimOpenSchema.IO;      // ReadBimDataFromParquetZip
using Ara3D.BimOpenSchema.IO.Usd;  // WriteUsda

var data = new FilePath("samples/public/duplex.bos").ReadBimDataFromParquetZip();
UsdExportSummary summary = data.WriteUsda("duplex.usda");
```

`WriteUsda(this IBimData, string path)` writes a temporary file beside `path` and moves it into
place only when the whole stage is written, so a failure leaves any earlier file untouched and
no partial `.usda`. `WriteUsda(this IBimData, TextWriter)` writes to a writer the caller owns.
Both return a `UsdExportSummary`: counts of materials, prototypes, declared parameter
properties, entities, elements (entities with geometry), instances, properties, relationship
targets, and of what was left out (instances with no mesh, relations with a bad end or type,
parameters with no value, duplicate parameters).

The project reads only `IBimData` (from `Ara3D.BimOpenSchema`) and the geometry helpers in
`Ara3D.BimOpenSchema.ObjectModel`; reading a `.bos` file is the caller's job.

## What the stage holds

Stage metadata: `upAxis = "Z"`, `metersPerUnit = 1`, `defaultPrim = "Model"`. BOS is already
z-up in metres, so nothing is converted.

```
/Model                         Xform, kind = assembly
  /Materials/M{i}              one UsdPreviewSurface material per BOS material row
  /Prototypes/P{i}/Mesh        one mesh per BOS mesh row, under an abstract ("class") scope
  /E_{GlobalId}                one prim per BOS entity, in entity order:
                                 Xform, kind = component, when it has geometry
                                 Scope when it has none (storeys, types, spaces without a shape)
      bim:... properties and relationships
      /I{k}                    BOS instance k: instanceable, references /Model/Prototypes/P{mesh}
  /Unassigned/I{k}             instances whose entity index is missing (only when there are some)
  /Descriptors                 each parameter property declared once, with its BOS strings
```

Every entity sits directly under `/Model`, so an entity's path depends only on its own
GlobalId (or index), never on its storey or type. Containment is a relationship, not nesting.

- **Entities without geometry are Scopes.** A Scope is USD's prim for grouping and holding
  data: it has no transform and is not imageable, so renderers and bounding-box computations
  skip it. An Xform would claim a placement that a storey or a type in BOS does not have. Only
  entities with geometry get `kind = "component"`, so selecting by kind in Omniverse picks
  building elements, not storeys or property sets.
- **Instancing.** Each BOS instance is a prim marked `instanceable = true` that references its
  mesh's prototype, so a mesh used by many instances is stored once. USD shares an instance's
  *descendants*, not the instance prim, which is why the mesh sits one level below `P{i}`.
  Instanceable references were chosen over a `PointInstancer` because a point instancer
  collapses every instance into one prim: elements could no longer be selected, hidden, or
  inspected one by one, and per-element properties would have nowhere to live.
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

### Entity prim names

`E_` followed by the GlobalId, with each character outside `[A-Za-z0-9_]` replaced by `_`
(IFC GlobalIds use `$`). When the GlobalId is missing, or an earlier entity already has the
same name (federated models can repeat GlobalIds), the prim is `E{entity index}` instead. All
names are assigned before writing, in entity order, so a relationship can point at an entity
written later. The real GlobalId is always in `bim:globalId`.

### Properties on an entity prim

| Property | USD type | Written when |
|---|---|---|
| `bim:entityIndex` | `int` | always: the BOS entity row |
| `bim:localId` | `int64` | the local id is not negative (-1 marks "none"): STEP line number (IFC) or ElementId (Revit) |
| `bim:globalId` | `string` | the GlobalId is present and not empty |
| `bim:name` | `string` | the name is present and not empty |
| `bim:category` | `string` | the entity has a category whose name is not empty |
| `bim:type` | `string` | the entity has a type whose name is not empty |
| `bim:document` | `string` | the entity's document has a title |
| `bim:param:{group}:{name}` | by parameter type | the entity has a value for that descriptor |
| `bim:{relation}` | `rel` | the entity is the source (EntityA) of that kind of relation |

Parameter types map as Int to `int`, Number to `float` (BOS numbers are 32-bit), String to
`string`, Point to `point3f`, and Entity to a relationship (`rel`) targeting the referenced
entity's prim.

A missing value is left out, never defaulted: a Number whose index is -1, a String index past
the end of the string table, an Entity reference whose target index is out of range. A value
BOS stores as an empty string is written as `""`. The summary counts what was left out.

### Relations

Each row of the BOS relations table becomes a target of a custom relationship on the source
entity's prim (EntityA), named after the relation type with a lower-case first letter:
`bim:partOf`, `bim:memberOf`, `bim:containedIn`, `bim:hostedBy`, `bim:childOf`,
`bim:hasLayer`, `bim:hasMaterial`, `bim:connectsTo`, `bim:hasConnector`, `bim:boundedBy`,
`bim:traverseTo`, `bim:voids`, `bim:fills`, `bim:covers`, and `bim:serves`. A Duplex door:

```
custom rel bim:containedIn = [</Model/E_1xS3BCk291UvhgP2dvNMKI>]   # Level 1, a Scope
custom rel bim:fills = [</Model/E_1xS3BCk291UvhgP2dvNrZJ>]         # its opening
```

Targets are in entity order; a relation listed twice in BOS is one target. A relation whose
source or target index is out of range, or whose type BOS does not define, is left out and
counted in `RelationsLeftOut`.

### Parameter names and the Descriptors scope

`{group}` and `{name}` are the descriptor's group and name made into identifiers the same way
as prim names (`Ifc:OverallWidth` becomes `Ifc_OverallWidth`; a leading digit gets a `_`).
When two descriptors would get one property name, the later one gets `_{descriptor index}`
appended. Entity prims carry values only; the exact BOS strings are on the declaration of the
same property under `/Model/Descriptors`:

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

`customData` also holds `string units` when the descriptor has units. In the first version
(prims for entities with geometry only), writing these strings on every prim instead made
Schependomlaan 32% larger (56.1 MB against 42.7 MB).

## Measured

Written on 2026-10-10 on the owner's Windows 11 machine, .NET 8, Release build, timing the
`WriteUsda` call alone (the BOS read is not included), three runs each; Duplex is the first
write in the test process and Schependomlaan the second. The numbers come from the output of
`SampleExportTests`. The `.usdc` and `.usdz` sizes come from converting with usd-core 0.26.8
as shown below.

| Model | BOS | Entities | Elements | Instances | Meshes | Properties | Relationship targets | `.usda` | Write time | `.usdc` | `.usdz` |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `duplex` | 0.1 MB | 4,721 | 236 | 681 | 435 | 39,553 | 723 | 4.5 MB | 55 ms | 0.74 MB | 0.74 MB |
| `schependomlaan` | 1.1 MB | 38,947 | 3,510 | 5,978 | 4,702 | 537,514 | 8,686 | 56.8 MB | 417 to 427 ms | 7.2 MB | 7.2 MB |

In the Schependomlaan text, parameter values are 49% of the bytes, identity properties 17%,
mesh points 13%, triangle indices 4%, the Descriptors scope 3%, and relations 1%. Most of its
38,947 entities are IFC geometry and quantity records (quantity counts, faceted breps, closed
shells, polylines, bounding boxes) that BOS keeps as entities; each is now a small Scope. usd-core 0.26.8 parses the `.usda` in 2.0 s and
opens the `.usdc` in 0.5 s. Gzip takes the `.usda` to 4.2 MB.

Text is about eight times the size of USD's binary crate format here. Scaled linearly to
Snowdon's roughly 450,000 instances (75 times Schependomlaan) that would be about 4.3 GB of
text and some 30 seconds of writing, against about 540 MB as `.usdc`. That is an
extrapolation, not a measurement, and Snowdon's ratio of entities to instances may differ. A
direct `.usdc` writer is the next step if models of that size need USD often. Until then,
convert after writing.

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
- Nest entities by containment; containment is the `bim:containedIn` relationship.
- Write normals, UVs, textures, or per-vertex colours; BOS has none of the last three.
- Convert colours between sRGB and linear.
- Merge several BOS models into one stage.
