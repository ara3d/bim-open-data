# Public sample buildings

Three openly licensed buildings, converted to BIM Open Schema (BOS) and to DuckDB, for demos, tests, and the BOS explorer in every BIM Open repository. They replace the Autodesk-derived models in `bim-open-schema/examples`, whose redistribution terms are unsettled (bim-open-toolkit TKT-144). Licences and attribution are in [`NOTICE.md`](NOTICE.md); anything that ships one of these files ships that notice too.

| File | Building | Source IFC | `.bos` | `.duckdb` |
|---|---|---|---:|---:|
| `schependomlaan` | Schependomlaan, a ten-apartment block (design model, Archicad) | IFC2X3, 49 MB | 1.1 MB | 3.4 MB |
| `digitalhub-arc` | DigitalHub, an office building of RWTH Aachen University: architecture (Revit) | IFC4 Reference View, 9 MB | 0.4 MB | 2.1 MB |
| `digitalhub-hzg` | DigitalHub: heating | IFC4 Reference View, 21 MB | 1.0 MB | 2.9 MB |
| `digitalhub-federated` | DigitalHub: architecture, heating, ventilation, and plumbing in one union, no geometry | four IFC4 files, 68 MB | 1.1 MB | 6.8 MB |
| `duplex` | Duplex Apartment, a two-unit house (Revit 2011) | IFC2X3, 2.4 MB | 0.1 MB | 1.8 MB |

Each `.bos` holds the entities, parameters, relations, and (except the federated union) tessellated geometry. Each `.duckdb` holds the same tables plus the text views `EntityText`, `ParameterText`, `RelationText`, `StoreyOfEntity`, and `StoreyOfElement`; it was written by DuckDB 1.3.2, so open it with 1.3.2 or later. The files are committed because each is under 10 MB.

## What each contains

Counts are IFC entities by class in `EntityText.Category`, measured on 2026-10-03. `PublicSampleTests` (in `tests/data/Ara3D.BimOpenSchema.DuckDb.Tests`) opens every file and checks these numbers against `samples.json`, so a regenerated sample that changes fails the build.

| | Schependomlaan | DigitalHub architecture | DigitalHub heating | DigitalHub federated | Duplex |
|---|---:|---:|---:|---:|---:|
| Entities (all rows) | 38,947 | 2,972 | 6,467 | 18,551 | 4,721 |
| Storeys | 6 | 3 | 3 | 12 (3 per model) | 4 |
| Spaces | 100 | 64 | | 64 | 21 |
| Doors | 205 | 64 | | 64 | 14 |
| Windows | 259 | 47 | | 47 | 24 |
| Walls (`IfcWall` + `IfcWallStandardCase`) | 652 + 282 | 178 | | 178 | 1 + 56 |
| Systems | | | 42 | 65 | |
| Pipe segments | | | 914 | 1,418 | |
| Pipe fittings | | | 743 | | |
| Space heaters | | | 63 | 63 | |
| Distribution ports | | | 3,760 | | |
| Port connections (`ConnectsTo` relations) | | | 1,873 | 4,208 | |
| Duct segments | | | | 595 | |
| Air terminals | | | | 148 | |
| Sanitary terminals | | | | 72 | |

Notes on the data:

- Schependomlaan's space names are lower-case Dutch, about ten of each apartment room (`woonkamer`, `keuken`, `badkamer`, `slaapkamer 1`). Its storeys run from `-1 fundering` to `04 dak`. The export carries quantity sets (7,193 `IfcQuantityCount` rows, and lengths, areas, and volumes) and 1,675 second-level space boundaries.
- DigitalHub's storeys are named by level code (`B01_OKRD` basement, `E00_OKRD` ground floor, `E01_OKRD` first floor; OKRD is *Oberkante Rohdecke*, top of the structural slab). Its spaces have German names (`Gruppenbüro 5`, `Treppenhaus Ost - EG`). Heating systems are named `H_Transport_VL_n` (flow) and `H_Transport_RL_n` (return), plus `H_WP_VL` and `H_WP_RL` for the heat pump.
- DigitalHub's architecture model declares metres; the heating, ventilation, and plumbing models declare millimetres. The federated union keeps each document's unit (`Ifc:LengthUnitToMetre` on its `IfcProject`) and does not rescale.
- The four DigitalHub models each have their own storeys and GlobalIds; the union does not match them across models, so storeys appear four times.

## Glossary

Dutch (Schependomlaan):

| Name | Meaning |
|---|---|
| woonkamer | living room |
| slaapkamer | bedroom |
| badkamer | bathroom |
| keuken | kitchen |
| berging | storage room |
| toilet | toilet |
| entree | entrance |
| overloop | landing |
| gang | corridor |
| kast | cupboard |
| mk | *meterkast*, the meter and fuse cupboard |
| instal. ruimte | installation (plant) space |
| onben. ruimte | *onbenoemde ruimte*, unnamed space |
| fundering | foundation |
| begane grond | ground floor |
| eerste, tweede, derde verdieping | first, second, third floor |
| dak | roof |

German (DigitalHub):

| Name | Meaning |
|---|---|
| Fachmodell (FM) | discipline model |
| ARC | architecture |
| HZG, Heizung | heating |
| LFT, Lüftung | ventilation |
| SAN, Sanitär | plumbing |
| VL, Vorlauf / RL, Rücklauf | flow / return |
| WP, Wärmepumpe | heat pump |
| UG / EG / OG | basement / ground floor / upper floor |
| Gruppenbüro | group office |
| Treppenhaus | stairwell |
| Flur, Fluchtflur | corridor, escape corridor |
| Schacht | shaft |
| Lager | storage |
| Heizzentrale | boiler room |
| RLT-Raum | air-handling plant room |
| Hausanschluss | utility service entrance |
| NSHV | main low-voltage switchboard |
| Seminarraum, Konferenzraum, Veranstaltungsraum | seminar room, conference room, event room |
| WC Damen / Herren | women's / men's toilet |
| Ost / West | east / west |

## Regenerate

```powershell
node samples/public/fetch.mjs     # downloads the source IFC files into samples/public/sources/ (git-ignored) and checks each SHA-256
node samples/public/convert.mjs   # builds the CLI, writes every .bos and .duckdb here
dotnet test tests/data/Ara3D.BimOpenSchema.DuckDb.Tests -c Release --filter "FullyQualifiedName~PublicSample"
```

`samples.json` is the one list of sources (URL, commit, size, SHA-256) and samples (inputs and expected counts); both scripts and the test read it. `convert.mjs` runs the `convert-ifc` and `federate-union` verbs of `tools/building-model-workflows`. A regenerated file differs byte for byte (zip timestamps, DuckDB layout) even when its tables do not, so commit one only when the converter changed.

## Use from another repository

The toolkit and `bim-open-flow` take this repository as `deps/bim-open-data`, so the samples are at `deps/bim-open-data/samples/public/<name>.bos` and `deps/bim-open-data/samples/public/<name>.duckdb`, with `NOTICE.md` beside them. The BOS explorer bundles `schependomlaan.bos`, `digitalhub-arc.bos`, `digitalhub-hzg.bos`, and `duplex.bos`.
