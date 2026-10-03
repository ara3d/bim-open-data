# BIM Open Data

![BIM Open Data](docs/brand/data-lockup.svg)

[`bim-open-schema`](https://github.com/ara3d/bim-open-schema) is the specification of BIM Open Schema; this repository is its .NET implementation: reading and writing BOS files, IFC loading, meshing, byte-exact property-set editing, conversion from IFC to BOS and to DuckDB, the IFC MCP server, and the BOS Browser.

**Status on 2026-10-03: the code is not here yet.** It lives in [`ara3d/bim-open-toolkit`](https://github.com/ara3d/bim-open-toolkit) under `src/data`, with its tests in `tests/data`, the MCP server in `src/mcp/BimOpenMcp.Ifc`, the BOS Browser in `apps/`, and the IFC type generator in `tools/Ara3D.IfcTypeGen`. It moves here, with its git history, in phase 4 of the toolkit's [repository split plan](https://github.com/ara3d/bim-open-toolkit/blob/main/docs/plans/repository-split.md). Until then this README describes what will arrive, and the toolkit is where to build, test, and file issues.

## What BIM Open Schema is

BIM Open Schema (BOS) stores a building model as plain tables instead of an object graph behind a vendor's API. Entities, parameters, relations, and geometry are each a list; each list is one Parquet file, and a `.bos` file is those Parquet files in a zip. Parameters are stored entity-attribute-value, one table per primitive type, so two parameters can share a name and differ in type. Relations use a closed vocabulary (`PartOf`, `ContainedIn`, `HostedBy`, `BoundedBy`) that covers both the Revit API and IFC (Industry Foundation Classes, the open exchange format for building models).

The tables load straight into DuckDB, an in-process analytical database, which is where most questions get answered.

## What it does not do

- It does not author geometry. IFC editing is limited to property sets.
- It has no live connection to Revit or any other authoring tool. Models arrive as IFC files or as BOS files from the Revit 2025 exporter, which stays in the toolkit's `plugins/`.
- It knows nothing about graphs or node packs. [BIM Open Flow](https://github.com/ara3d/bim-open-flow) builds on it, never the other way round.

## Projects

These are the 21 projects in the toolkit's `src/data` on 2026-10-03, grouped by what they do. Most target `net8.0-windows` because the IFC loader does; the schema libraries target plain `net8.0`.

### BIM Open Schema

| Project | Role |
|---|---|
| `Ara3D.BimOpenSchema.ObjectModel` | Builders, accessors, and a navigable object graph over the specification's types |
| `Ara3D.BimOpenSchema.IO` | Reading and writing `.bos` archives (Parquet in a zip); Parquet.Net is its only external dependency |
| `Ara3D.BimOpenSchema.IO.Export` | Excel, CSV, Markdown, HTML, and SQLite exports of a table |
| `Ara3D.BimOpenSchema.IO.Bfast` | BFAST serialization of BOS data |
| `Ara3D.BimOpenSchema.DuckDb` | Loading BOS into DuckDB, views, queries, and table helpers |
| `Ara3D.BimOpenSchema.Harmonizer` | Canonical names and SI units across models from different tools |
| `Ara3D.BimOpenSchema.Federation` | Unions several BOS documents into one geometry-free BOS and a DuckDB database |
| `Ara3D.BimOpenSchema.DataModel`, `.DataModel.IO` | A relational snapshot model with validation and a spatial index |
| `Ara3D.BimOpenSchema.BuildingModel`, `.Source`, `.Workflows`, `.Workflows.IO`, `.DuckDb` | A typed building model with source mapping and a DuckDB projection |

### IFC

| Project | Role |
|---|---|
| `Ara3D.IfcTypes` | Generated IFC entity types, produced by `Ara3D.IfcTypeGen` from a Parakeet grammar |
| `Ara3D.IfcLoader` | STEP parsing, with geometry from web-ifc |
| `Ara3D.Ifc.Mesher` | Tessellation in pure C# |
| `Ara3D.Ifc.Editing` | Property-set patching that locates each entity by byte range, so every byte you did not edit comes out identical |
| `Ara3D.Ifc.Bos` | IFC to BOS conversion |
| `Ara3D.Ifc.DuckDb` | One call from an IFC file to a queryable DuckDB database: convert to BOS, load the tables, create the text views |
| `Ara3D.Ids` | Evaluates buildingSMART IDS 1.0 specifications over BOS tables in DuckDB, as a verdict table |

### Applications and servers

| Project | Role |
|---|---|
| `BimOpenMcp.Ifc` | An MCP (Model Context Protocol) server with 29 tools for an AI agent: entities, property sets, quantities, spatial structure, SQL over DuckDB, geometry bounds and volumes, GLB export, and IFC to BOS. Runs over stdio, or HTTP with `--http <port>`. |
| `Ara3D.BimOpenSchema.Browser` | A WPF data-grid viewer for `.bos` files with glTF and Excel export (`net10.0-windows`) |
| `Ara3D.IfcTypeGen` | The generator behind `Ara3D.IfcTypes` |

Eleven test projects come with them from `tests/data`.

## Dependencies

After the move, this repository will list its dependencies in `deps.json` and read them from a git-ignored `deps/` folder that `node deps.mjs` fills, as the viewer already does:

- [`bim-open-schema`](https://github.com/ara3d/bim-open-schema), the specification;
- [`ara3d-sdk`](https://github.com/ara3d/ara3d-sdk), general .NET utilities and the MCP protocol helpers;
- [`parakeet`](https://github.com/ara3d/parakeet), the parser library the IFC type generator uses.

Outside packages include Parquet.Net, DuckDB.NET, ClosedXML for Excel, and Xbim.InformationSpecifications for IDS; the IFC loader carries the native web-ifc library.

## Prerequisites (after the move)

The .NET 8 SDK, plus the .NET 10 SDK for the BOS Browser. The IFC stack and the Browser target Windows, so the full repository builds on Windows only.

## Web page

`site/index.html` is the repository's page, deployed to `https://ara3d.github.io/bim-open-data/` by `.github/workflows/pages.yml` on each push to `main` that changes `site/`. The owner must first switch Pages on in this repository's settings (Settings, Pages, Source: GitHub Actions); until then the workflow's deploy step fails.

## The family

BIM Open Data is one of the BIM Open repositories, alongside [BIM Open Flow](https://github.com/ara3d/bim-open-flow) (graphs over the tables), [BIM Open Viewer](https://github.com/ara3d/bim-open-viewer) (the 3D viewer), [BIM Open Notebook](https://github.com/ara3d/bim-open-notebook) (agent sessions as documents), and [BIM Open Toolkit](https://github.com/ara3d/bim-open-toolkit), which joins them. The mark and colours follow the toolkit's `docs/BRANDING.md`.

## License

MIT. See `LICENSE`.
