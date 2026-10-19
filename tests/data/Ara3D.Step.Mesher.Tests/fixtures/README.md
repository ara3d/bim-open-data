# Fixtures

STEP files the mesher tests read. Each entry names its source and licence.

- `unit-cube.stp`, `two-cubes-assembly.stp`, `unit-cube-truncated.stp`: hand-written for the Plato STEP library's adapter spec,
  bim-open-toolkit `sandbox/step-brep-mesh/adapter/fixtures/` (commit b93f6da0); copied byte-exact.
  `unit-cube-truncated.stp` stops inside #125 (byte 4232); `.gitattributes` keeps it byte-exact.
- `unit-cylinder.stp` (radius 1 mm, height 1 mm, one seam edge) and `unit-sphere.stp` (radius 1 mm, one face bounded by a
  half-circle seam between its poles, as OpenCascade writes a sphere): hand-written for TKT-313's Plato bridge tests,
  with the unit cube's units, context and product.
