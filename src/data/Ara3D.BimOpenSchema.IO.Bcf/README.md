# Ara3D.BimOpenSchema.IO.Bcf

Writes BCF 3.0 (BIM Collaboration Format, buildingSMART's issue-exchange format)
from a table of issues, so the failing rows of a rule check, an IDS verdict
table, or any SQL result over a BIM Open Schema (BOS) model open as topics in
Revit, Solibri, BIMcollab, and other BCF readers.

Depends only on the spec types (`Ara3D.BimOpenSchema`) and `Ara3D.DataTable`.

## Calling it

```csharp
using Ara3D.BimOpenSchema.IO.Bcf;

// Any IDataTable with a GlobalId column and a Title column; one topic per distinct title.
IDataTable doors = conn.Query("""
    SELECT GlobalId, 'Doors of type ' || Type AS Title
    FROM EntityText WHERE Category = 'IFCDOOR'
    """);

var bounds = ElementBounds.FromBimData(bimData);   // optional: places a camera per topic
var summary = BcfWriter.WriteFile("doors.bcf", doors.ToBcfIssues(), new BcfOptions(), bounds);
```

- **Input.** `ToBcfIssues(table, columns)` reads the columns named by
  `BcfColumns` (case-insensitive): `GlobalId` and `Title` are required;
  `Description`, `Status`, `Priority`, and `Type` are optional. Rows group by
  title in order of first appearance. A topic lists each GlobalId once, and
  joins its rows' distinct descriptions with line breaks. Status, priority, and
  type must agree within a title. `BcfColumns.Verdicts` reads a verdict table
  (`checkTitle`, `citation`). Filter it to failing rows first, because every
  row becomes part of a topic. A list of `BcfIssue` records works too.
- **Viewpoints.** With `ElementBounds`, each topic whose elements have geometry
  gets one `viewpoint.bcfv`. The viewpoint selects the elements, keeps the rest
  of the model visible, and holds a perspective camera that frames their
  bounding box from the south-east, above. Each element's box comes from
  `BosScene.Bounds` in Ara3D.BimOpenSchema.ObjectModel, the view the GLB, USD and
  BCF writers share: its visible instances, each mesh's box moved by the
  instance's transform. `ElementBounds.FromScene` takes a scene a caller keeps.
  Coordinates are BOS's z-up metres,
  which are the IFC file's own coordinates and so also BCF's. A 22-character IFC
  GlobalId is written as the component's `IfcGuid`. Any other id, such as a
  Revit UniqueId, is written as `AuthoringToolId`, because BCF rejects it as an
  `IfcGuid`.
- **No geometry, no camera.** BCF 3.0 requires a camera in every viewpoint
  (`visinfo.xsd`), and this writer does not invent one. A topic with no
  geometry for any of its elements therefore has no viewpoint. Its GlobalIds
  are appended to its description, the only other place BCF can hold them.
  `BcfSummary.ElementsWithoutGeometry` counts such elements.
- **Determinism.** Topic GUIDs are name-based (UUID version 5) from
  `BcfOptions.GuidSeed` and the title. Viewpoint GUIDs come from the topic's.
  `BcfOptions.CreationDate` stamps every topic and zip entry. Fix the date and
  the same input gives the same bytes. Giving a rerun check the same seed keeps
  its topic GUIDs, so a reader updates those topics instead of adding new ones.
- **Text.** A character XML 1.0 cannot hold is written as U+FFFD, the
  replacement character, in every field. That covers control characters other
  than tab and line breaks, U+FFFE, U+FFFF, and unpaired surrogates. An IFC
  `\X\02` escape in a name, for example, decodes to U+0002.
- **Errors.** A missing required column, a blank title, two issues with the
  same title (after trimming), or two priorities within one topic throw
  `ArgumentException` with the offending name. Every topic is checked before
  anything is written. `WriteFile` writes a temporary file beside the target and
  moves it into place only when complete, so a failure leaves an existing file
  as it was.

The container holds `bcf.version`, `extensions.xml` (the topic types,
statuses, and priorities used), and one folder per topic named by its GUID, each
with `markup.bcf` and possibly `viewpoint.bcfv`. Folder entries are written as
the BCF encoding guide asks.

## What it does not do

- Read BCF, or talk to a BCF API server.
- Comments, snapshots (no renderer here), clipping planes, colouring,
  `project.bcfp`, `documents.xml`, labels, assignees, due dates, or the
  markup header naming the model file.
- Warn when a topic selects more than 1,000 components, where the BCF guide
  asks readers to alert the user.
- Map a Revit UniqueId to the ElementId that Revit reads as `AuthoringToolId`.

## Measured on duplex

`samples/public/duplex.bos` has 14 doors of 4 types. One warm run inside the
test host, using the door query above, measured these numbers:

| Step | Result |
|---|---|
| Read the `.bos` | 29 ms |
| `ElementBounds.FromBimData` | 4 ms; 215 GlobalIds with geometry from 681 instances |
| SQL in DuckDB | 45 ms; 14 rows |
| Write with bounds | 4 ms; 4 topics, 4 viewpoints, 14 elements, 5,204 bytes |
| Write without bounds | 4 topics, no viewpoints, 3,100 bytes |

Door boxes measure 2.09 to 2.50 m high and 0.91 to 1.40 m wide, frames
included. Every written file validates against the BCF 3.0 XSDs (see
`tests/data/Ara3D.BimOpenSchema.IO.Bcf.Tests`).
