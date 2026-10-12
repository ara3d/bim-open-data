# Ara3D.Ids.Tests

Facet-by-facet tests over a small model built from the text tables
(`IdsTestModel`: doors, walls, a window, a space, and, for the material and
part-of facets in `IdsRelationFacetTests`, a layer set, materials, a storey and
a building with their relations), the table shapes, and the committed IDS files
under `ids/` over `samples/public/duplex.bos`. Over the Duplex, every IFCDOOR is
contained in an IFCBUILDINGSTOREY (14 pass), and 15 of the 56
IFCWALLSTANDARDCASE walls have a "Masonry - Concrete Block" layer; the doors
have no material.

`parity/run-ifctester.py` runs IfcOpenShell's ifctester over the source IFC and
prints pass and fail counts per specification; `DuplexIdsTests.DcW1_AgreesWithIfctester`
compares them to the evaluator's and skips when Python or ifctester is missing.

Last recorded run, 2026-10-11, ifcopenshell 0.8.5 over `Duplex_A_20110907.ifc`:

```
DC-W1 pass=8 fail=6
IDS-OC pass=0 fail=218
IDS-EC pass=0 fail=218
```

The evaluator gives the same counts on `duplex.bos`.
