# Per-product discrepancy tables

One table per quick file, written by `DiscrepancyTableTests` (`tests/data/Ara3D.IfcMeshingComparison`) from the committed dumps in `../dumps` and the two in-process meshers (Ara3D.Ifc.Mesher and the web-ifc DLL). Each row is one product (express id) with, per mesher, triangle count, shape score against the majority, signed volume, and bounds. The majority and the shape score are defined in `Harness/DiscrepancyTableConsensus.cs`; the shape score is `ModelComparer`'s entity-shape metric run on one product. Rows are sorted by how far Ara3D.Ifc.Mesher is from the majority.

| File | Products | all-agree | ours-agrees | ours-differs | missing-in-ours |
|---|---:|---:|---:|---:|---:|
| `example.md` | 116 | 80 | 8 | 28 | 0 |
| `steelplates.md` | 16 | 10 | 0 | 4 | 2 |
| `IfcOpenHouse_IFC4.md` | 39 | 32 | 0 | 3 | 4 |

## What the tables show

**example.ifc bounds against web-ifc.** No product in `example.md` has bounds more than 5 % from web-ifc's (DLL or Node build), or from IfcLite's or IfcOpenShell's, using the `BoundsClose` test the harness uses. The largest difference over the 116 products rounds to 0.000. So the one product in the old 115-of-116 bounds count does not appear in these dumps, and `Example_SharedEntityBoundingBoxesMatchOracle` passed when run alone on 2026-10-11. One product, #9989 (IFCSPACE), has no geometry in the web-ifc Node dump; the other four meshers have it.

**steelplates.ifc: 4 of 14 products differ in shape.** Of the 16 products, 14 have geometry in ours; the 4 that differ (shape score 0.88 to 0.89 against the majority):

| Id | Product | Geometry items |
|---:|---|---|
| 1193 | IFCBEAM | IFCBOOLEANCLIPPINGRESULT, IFCBOUNDINGBOX |
| 1473 | IFCCOLUMN | IFCBOUNDINGBOX, IFCEXTRUDEDAREASOLID |
| 633 | IFCBEAM | IFCBOOLEANCLIPPINGRESULT, IFCBOUNDINGBOX |
| 1385 | IFCBEAM | IFCBOOLEANCLIPPINGRESULT, IFCBOUNDINGBOX |

The other two products, #1267 and #1681 (IFCOPENINGELEMENT), have no geometry in ours, and none in the web-ifc DLL or the web-ifc Node dump either; IfcLite and IfcOpenShell have them.

**example.ifc: 28 of 116 products differ in shape, by geometry entity kind.**

| Geometry items | Products | Product kinds | Shape score |
|---|---:|---|---|
| IFCEXTRUDEDAREASOLID + IFCPOLYLINE | 18 | 17 IFCBEAM, 1 IFCSPACE (#9989) | 0.74 |
| IFCEXTRUDEDAREASOLID | 9 | IFCCOLUMN | 0.75 to 0.90 |
| IFCBOOLEANCLIPPINGRESULT + IFCPOLYLINE | 1 | IFCWALLSTANDARDCASE (#12954) | 0.89 |

**IfcOpenHouse_IFC4.ifc.** Four IFCOPENINGELEMENT products (#119, #141, #311, #2380) are missing in ours and in both web-ifc builds. Three walls differ in shape (0.87 to 0.89): #268 and #281 (IFCBOOLEANCLIPPINGRESULT + IFCPOLYLINE) and #40 (IFCEXTRUDEDAREASOLID + IFCPOLYLINE).
