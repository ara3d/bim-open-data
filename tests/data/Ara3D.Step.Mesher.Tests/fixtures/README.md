# Fixtures

STEP files the mesher tests read. Each entry names its source and licence.

- `unit-cube.stp`, `two-cubes-assembly.stp`, `unit-cube-truncated.stp`: hand-written for the Plato STEP library's adapter spec,
  bim-open-toolkit `sandbox/step-brep-mesh/adapter/fixtures/` (commit b93f6da0); copied byte-exact.
  `unit-cube-truncated.stp` stops inside #125 (byte 4232); `.gitattributes` keeps it byte-exact.
