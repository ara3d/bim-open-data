# Ara3D.IfcTypeGen

Generates the entity, interface, enum, alias, and schema source files in
`src/data/Ara3D.IfcTypes` from the buildingSMART EXPRESS schema files, using the
Parakeet EXPRESS grammar. It also reads the STEP long forms (AP242, AP214, AP203)
into the same schema model; generating `Ara3D.StepTypes` from them is TKT-304.

Parakeet comes from `deps/parakeet` (`ara3d/parakeet`, pinned in `deps.json`),
which also carries the IFC schema files under `input/exp` (`IFC2X3.exp`,
`IFC4.exp`, `IFC4X3.exp`) and the test helpers that locate them. The generator
is an explicit NUnit test, so it runs on demand and never in a normal test pass:

```bash
dotnet test tools/Ara3D.IfcTypeGen --filter TestGenerate
```

It takes about half a minute and overwrites the `*.g.cs` files in
`src/data/Ara3D.IfcTypes`. Run it after changing the grammar, the reader, or the
schema files, then review the diff; on an unchanged input the output is
byte-identical.

| File | Holds |
|---|---|
| `ExpressSchema.cs` | The schema model: entities with all their supertypes, own explicit attributes, and DERIVE redeclarations |
| `ExpressReader.cs` | EXPRESS text to the model; throws on any block the grammar reads only in part |
| `ExpressAttributeLayout.cs` | Part 21 attribute positions over the whole supertype graph, for simple and complex instances |
| `StepSchemaFiles.cs` | Finds the STEP schemas under `data/express/` |
| `IfcCodeGenTest.cs` | The IFC generator (`TestGenerate`) |
| `StepSchemaLayoutTests.cs` | Attribute order of AP242 entities against the records in the toolkit's `sandbox/step-brep-mesh/research/STEP-BREP-MODEL.md` section 3 |

The STEP schemas state no licence and are never committed. Fetch them with
`pwsh data/get-express-schemas.ps1`; the STEP tests are skipped until then.
