# Ara3D.BimOpenSchema.ObjectModel.Tests

NUnit tests for [Ara3D.BimOpenSchema.ObjectModel](../../../src/data/Ara3D.BimOpenSchema.ObjectModel).

```
dotnet test tests/data/Ara3D.BimOpenSchema.ObjectModel.Tests -c Release
```

- `BosSceneTests` builds a six-entity model by hand to cover `BosScene`, the view the GLB, USD
  and BCF writers share: instance indices out of range (mesh, transform, material, entity),
  short columns, hidden instances, an empty mesh, the filter and its counts, grouping by
  entity, bounds under a rotation and a scale, GlobalIds and names that are empty or missing,
  and typed parameter values that are missing or of another type. The writers' own tests run
  the view over the committed samples.
