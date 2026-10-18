# Ara3D.Step.Decode

Decodes a parsed STEP file into the Plato library's `RawDecodedModel`: the B-rep as the file writes it, every
occurrence placed in millimetres and radians, ready for Plato's `PrepareModel` (`Ara3D.StepBrep`). It is the host
obligation `DecodeRaw` of bim-open-toolkit `sandbox/step-brep-mesh/03-step/step.library.plato`, written against that
layer's README section "Boundary moved to RawDecodedModel, 2026-10-17". It reads the file only through the generated
AP242 descriptors (`Ara3D.StepTypes`, through `Ara3D.Step.Entities`).

```csharp
using var document = new StepDocument(path);
RawDecodedModel raw = document.DecodeRaw();     // never throws on a bad file
DecodedModel finished = raw.PrepareModel();     // Plato: tolerances, repairs, folds, closure
```

What the decoder does and leaves to Plato:

- **Structural mapping only.** One kernel row per reached vertex_point, edge_curve, loop, face, shell, body, curve,
  pcurve, 2D curve and surface; `SameSense` and bound orientations as written; periodicity by family (no closure
  mark); tolerances unset; no vertex merged, no edge dropped.
- **Units.** Every 3D context's units and largest length uncertainty (`ContextUnits`); lengths to millimetres and
  angles (conic parameters and trims, cone semi-angles, angular pcurve coordinates) to radians per context;
  `LengthFactor` and `AngleFactor` of occurrence 0's context.
- **Occurrences.** Products from each root down, placed by item_defined_transformation or
  cartesian_transformation_operator_3d (sides decided by membership), mapped items composed to any depth, cycles cut;
  roots first. Rows are in ascending instance id within each occurrence, occurrences in order.
- **Folds, not flips.** An oriented_face or oriented shell with `.F.` is a `FoldRecord`; Plato applies it.
- **Diagnostics, not exceptions.** Parser findings, the census of ignored and unsupported types, unit findings, and
  every instance that cannot be decoded (skipped with the rows that need it, as a `Skipped` repair) use the codes of
  `03-step/step-diagnostics.library.plato`.

| File | Holds |
|---|---|
| `StepDecoder.cs` | `DecodeRaw`: the order of the steps and the unreferenced census |
| `StepCensus.cs` | Ignored and unsupported types, by the library's own schema table |
| `ContextUnitsReader.cs`, `StepUnitFactors.cs` | Units and uncertainty per context |
| `StepLinks.cs`, `OccurrenceFinder*.cs`, `FoundOccurrence.cs` | The inverse links and the occurrence walk |
| `OccurrenceReader*.cs` | One occurrence's topology, curves, surfaces and parameter-plane curves |
| `PendingRows.cs`, `KernelTables.cs`, `KernelKinds.cs` | Reading each row once, indices, the model's tables |
| `StepFrames.cs`, `Affine.cs`, `Vec3.cs`, `SplineKnots.cs`, `StepSelectReaders.cs` | Points, frames, maps, knots, trims |
| `DecodeFindings.cs`, `DecodeException.cs` | Diagnostics, and the exception that stops one row |

Not decoded yet (each is a skipped instance with a diagnostic): a trim given only by points, a conic in a
parameter plane scaled unevenly (a degree file's cylinder), a pcurve as an edge's curve, `face` without a
surface. The `closed_curve`, `u_closed` and `v_closed` flags are not carried (the type has no field for them).
