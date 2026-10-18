# Ara3D.StepBrep

The Plato STEP B-rep library (bim-open-toolkit `sandbox/step-brep-mesh`, layers 00 to 07) compiled to C# by Plato's
compiler: `BrepModel` and its tables, `RawDecodedModel`, `PrepareModel`, the witnesses (`Witness_RawUnitCube`,
`Witness_RawTwoCubes`), the comparisons (`SameRawModel`, `SameDecodedModel`) and every law as a Boolean function.
`Ara3D.Step.Decode` fills a `RawDecodedModel` from a STEP file; this project finishes and checks it.

Nothing here is written by hand except `BodilessStubs.cs` (one stub the C# writer does not emit yet, see the file).
Regenerate with `tools/regen-step-brep.ps1`; the test `Ara3D.Step.Decode.Tests.StepBrepGenerationTests` fails when
the committed files differ from a fresh run.

| Folder or file | Holds |
|---|---|
| `Generated/*.g.cs` | Plato's output for the stdlib's foundation and geometry tiers and the library's layers 00 to 07, `*.spec.plato` left out; each file's first line names the Plato and library commits |
| `Intrinsics/*.cs` | A copy of Plato's handwritten runtime (`src/Plato.Intrinsics`), which the generated structs fuse with |
| `BodilessStubs.cs` | The stub of `FlatRecord` until Plato's plato-459 lands |

Choices the script makes:

- **Number is binary64** (`--profile=cpu64`, Plato b875bbc0); `Generated/NumberProfile.g.cs` stops a build that defines
  `PLATO_NUMBER_GPU32`.
- **The namespace is `Ara3D.StepBrep`**, not the `Ara3D.Geometry` Plato writes, so a project can reference this one and
  the `Ara3D.Geometry` package together (TKT-311 met CS0433 between their two `Point3D`s). The generated
  `IReadOnlyList<T>` extensions (`Any`, `Take`, `Map`, ...) can still meet LINQ's (CS0121) in a file that imports both
  namespaces; call `Enumerable.Any(...)` there.
- **Left out:** layers 08 and 09 (nothing in 00 to 07 needs them; 09 has a writer error, CS0305), the stdlib's graphics
  tier, its `brep.*` files (the library declares its own B-rep types), and `remeshing.*` and `lattices.*`, which hold
  two stdlib defects (CS0117, CS1929; TKT-315).

As of the first generation (Plato 54a3996a, library a5df350e), `PrepareModel` throws `NotImplementedException`: it
calls `Reduced` and `ToleranceDiagnostics`, which `03-step/step.library.plato` declares without bodies (TASKS.md
chunk D gives them bodies).
