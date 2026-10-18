# Ara3D.IfcTypeGen

Generates the entity, interface, enum, alias, and schema source files in
`src/data/Ara3D.IfcTypes` from the buildingSMART EXPRESS schema files, and the
descriptor, interface, enum, and schema files in `src/data/Ara3D.StepTypes` from
the STEP AP242 long form, using the Parakeet EXPRESS grammar. It also reads the
AP214 and AP203 long forms into the same schema model, for the layout tests.

Parakeet comes from `deps/parakeet` (`ara3d/parakeet`, pinned in `deps.json`),
which also carries the IFC schema files under `input/exp` (`IFC2X3.exp`,
`IFC4.exp`, `IFC4X3.exp`) and the test helpers that locate them. The generator
is an explicit NUnit test, so it runs on demand and never in a normal test pass:

```bash
dotnet test tools/Ara3D.IfcTypeGen --filter TestGenerate
```

It takes about half a minute and overwrites the `*.g.cs` files in
`src/data/Ara3D.IfcTypes`. The STEP generator is `StepCodeGenTest.TestGenerate`
(`--filter StepCodeGenTest.TestGenerate`, about 15 s) and overwrites the `*.g.cs`
files in `src/data/Ara3D.StepTypes`; `StepCodeGenTest.GeneratedFilesAreCurrent`,
a normal test, fails when they differ from what the generator would write. Run
the generators after changing the grammar, the reader, or the schema files, then
review the diff; on an unchanged input the output is byte-identical.

| File | Holds |
|---|---|
| `ExpressSchema.cs` | The schema model: entities with all their supertypes, own explicit attributes, and DERIVE redeclarations |
| `ExpressReader.cs` | EXPRESS text to the model; throws on any block the grammar reads only in part |
| `ExpressAttributeLayout.cs` | Part 21 attribute positions over the whole supertype graph, for simple and complex instances |
| `StepSchemaFiles.cs` | Finds the STEP schemas under `data/express/` |
| `IfcCodeGenTest.cs` | The IFC generator (`TestGenerate`) |
| `StepCodeGenTest.cs` | The STEP generator (`TestGenerate`) and the check that its output is committed |
| `StepCodeEmitter.cs` | The STEP output: per entity an interface and a descriptor class with every slot of its record, per SELECT an interface, per ENUMERATION an enum |
| `StepTypeMap.cs` | EXPRESS types to the C# type of a STEP attribute descriptor |
| `StepNames.cs` | Pascal-case names, and field names when two supertypes declare one attribute name |
| `StepSchemaLayoutTests.cs` | Attribute order of AP242 entities against the records in the toolkit's `sandbox/step-brep-mesh/research/STEP-BREP-MODEL.md` section 3 |

The STEP schemas state no licence and are never committed. Fetch them with
`pwsh data/get-express-schemas.ps1`; the STEP tests are skipped until then.
