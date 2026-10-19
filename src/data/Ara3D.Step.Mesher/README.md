# Ara3D.Step.Mesher

Turns a STEP file (AP203, AP214 or AP242, ISO 10303-21) into a placed, coloured `Ara3D.Models.Model3D`, the way
`Ara3D.Ifc.Mesher`'s `IfcMesher.Build` does for IFC. Pure C#, `net8.0`. It reads through the generated AP242 types
(`Ara3D.StepTypes`, via `Ara3D.Step.Entities`), so dispatch is by type test (`e.Is<IMappedItem>()`), never by entity
name.

```csharp
var result = StepMesher.Mesh(@"C:\models\part.step");   // or StepMesher.Mesh(stepDocument, options)
// result.Model, result.Placements, result.ContextUnits, result.ParseDiagnostics, result.Prefix, ...
```

## What it reads

| Step | Where | Entities |
|---|---|---|
| Units | `Model/StepUnitsReader.cs`, `StepContextUnits.cs` | the context's `global_unit_assigned_context` length unit: `si_unit` with prefix, or a `conversion_based_unit` followed through its factor (inch, thou); `global_uncertainty_assigned_context`, smallest length uncertainty, in file units |
| Placements | `Model/StepPlacements.cs` | `axis2_placement_3d` (ISO build_axes), `cartesian_transformation_operator_3d` (scale, mirror) |
| Shapes | `Model/StepShapeCollector.cs` | items of every `shape_representation` subtype; `shape_representation_relationship` without a transformation brings in the B-rep representation; `mapped_item` places a `representation_map` origin onto target, sharing the mesh |
| Assemblies | `Model/StepShapeCollector.Assembly.cs` | `product_definition` → `next_assembly_usage_occurrence` → `context_dependent_shape_representation` → `representation_relationship_with_transformation` → `item_defined_transformation`, composed from the root down, one instance per occurrence |
| Colours | `Model/StepColours.cs`, `StepStyles.cs` | `styled_item` → `presentation_style_assignment` → `surface_style_usage` → `surface_side_style` → `surface_style_fill_area` → `fill_area_style` → `fill_area_style_colour` → `colour_rgb` or `draughting_pre_defined_colour`; `surface_style_rendering` colour and transparency |
| Meshing | `StepMesher.cs`, `Plato/StepPlatoMesher.cs`, `Model/StepModelBuilder.cs` | the whole file meshed once by `StepPlatoMesher.MeshFile`; each placed item's mesh taken from it by `StepShapeMesher.MeshItem` |

`StepMeshingResult` carries the model and its counts and bounds (metres), every placement (meshed or not), the units
of each context, the assembly depth, cycles refused, the meshing diagnostics (entity counts, worst support status,
messages), the parser's `StepDocument.Diagnostics`, and `Prefix` when the parser stopped at an error: a truncated file
is meshed as far as it was read.

## Pending, approximate and unsupported

- **Geometry comes from the Plato STEP library compiled to C#** (`Ara3D.StepBrep`; library in bim-open-toolkit
  `sandbox/step-brep-mesh`, TKT-299 and TKT-313): `StepMesher.Mesh` meshes the whole file once through
  `Plato/StepPlatoMesher.MeshFile`, and `StepShapeMesher.MeshItem` takes each item's mesh from it by its body's
  instance id. Faces whose library code is not written yet (spheres, tori, cones and B-splines in the committed
  generation) are dropped and recorded; an item all of whose faces are pending counts in `GeometryPending`.
- **One colour per item.** `Model3D` gives an instance one material, so a solid whose faces carry several colours
  takes the most common one, recorded as approximate. Per-face colour needs a mesh split by colour.
- **Occurrence colours** (`over_riding_styled_item` on an assembly occurrence) and invisibility are not read.
- **A context with no length unit** is read as millimetres, recorded as approximate.
- **An occurrence with no placement** is placed at its assembly's origin (approximate), unless a `mapped_item` of the
  assembly already placed it (Unigraphics writes assemblies that way).
- **Representations no product reaches** are placed at the origin, unless they repeat an item already placed (a
  `shape_aspect`'s representation repeats its part's solid).
- **Not read:** tessellated items (`tessellated_*`), wireframe and `geometric_set` items, CSG and swept solids
  (counted as unsupported), and AP242 external references.
- **Cycles** in assemblies or mapped items are recorded in `Cycles` and not followed.

## Tests

```
dotnet test tests/data/Ara3D.Step.Mesher.Tests --artifacts-path artifacts/builders/<name>
```

Fixtures are in `tests/data/Ara3D.Step.Mesher.Tests/fixtures` (sources in its README); the model tests in `Model/`
also build STEP text inline.
