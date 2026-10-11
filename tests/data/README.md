# tests/data

NUnit projects for `src/data`, one per library where there is coverage, plus
two harnesses: `Ara3D.DoorClearance.Tests` (a rules-based compliance check
over IFC) and `Ara3D.IfcMeshingComparison` (the pure C# mesher over model
files and against web-ifc; CI runs it with the solution, and every test
ignores itself when its model file is absent). `Ara3D.Ifc.Mesher.Tests`
holds the mesher's fast tests, built from STEP snippets with no model files.

Fixtures resolve from the repository `data/` folder (populate it with
`data/get-test-data.ps1`); tests that need a file they cannot find are skipped
or marked inconclusive rather than failed.
