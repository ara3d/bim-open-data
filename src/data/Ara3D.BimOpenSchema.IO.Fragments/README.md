# Ara3D.BimOpenSchema.IO.Fragments

Reads That Open's Fragments 2 files (`.frag`, the FlatBuffers format of the
`@thatopen/fragments` 3.x web BIM viewers) into BIM Open Schema (BOS) data, so a
model someone has only as a `.frag` can be queried like any `.bos`.

```csharp
BimData data = FragmentsToBos.Read("model.frag");        // in memory
FragmentsToBos.Convert("model.frag", "model.bos");       // to a .bos archive
```

Entities, parameters, and relations are read; geometry is not read yet (the
geometry tables are empty).

A file that is not Fragments 2 throws `FragmentsFormatException`, whose message
names what was found: a file identifier other than `0001`, a writer version
other than 3.x, or a buffer that does not hold a `Model` root.

## The schema

`Schema/index.fbs` is That Open's schema copied byte for byte from
[ThatOpen/engine_fragment](https://github.com/ThatOpen/engine_fragment)
`packages/fragments/flatbuffers/index.fbs` at tag `v3.4.8`, commit
`2ba7e9ddbc02689b8c1044e31daed26f3005f3c5` (the file last changed in
`ff8bf3bcd78526f8fe49a73ed7058485e85e3d87`, which added `ModelIndex`). It is MIT
licensed by That Open Company; the licence is `Schema/LICENSE-ThatOpen.md`.

`Schema/Fragments_generated.cs` is generated, and committed. To regenerate it,
download flatc 25.2.10 from the
[google/flatbuffers release](https://github.com/google/flatbuffers/releases/tag/v25.2.10)
and run `node Schema/generate.mjs <path to flatc>`. The script puts the types in
the namespace `Ara3D.BimOpenSchema.IO.Fragments.Schema` (the schema declares
none, and its `Material`, `Transform`, and `Attribute` would collide with
others). The `Google.FlatBuffers` package version must equal the flatc version:
the generated code calls `FlatBufferConstants.FLATBUFFERS_25_2_10`.

## What a .frag holds, and how it is read

Facts below were read from the `@thatopen/fragments` 3.4.8 TypeScript source
(`IfcImporter`, `IfcPropertyProcessor`, the model loader) and checked on the
Duplex fixture.

- **Compression.** The importer zlib-deflates the whole buffer (pako's default,
  header `78 9C`) unless asked for raw output. The reader inflates when the
  first two bytes are a zlib header, the test That Open's loader uses.
- **No file identifier.** `index.fbs` declares `file_identifier "0001"`, but the
  importer finishes the buffer with `builder.finish(model)`, which writes none.
  The reader accepts a buffer with no identifier or with `0001`.
- **Version.** Since 3.4 the metadata JSON carries `generator` and `version`
  (`"@thatopen/fragments"`, `"3.4.8"`). A version whose major number is not 3 is
  refused; a file without one is read.
- **Verifier.** `Google.FlatBuffers` 25.2.10's `Verifier` throws
  `OverflowException` (in `GetVRelOffset`) on the Duplex fixture, which That
  Open's own reader opens. The reader checks the root offset and the required
  root fields instead; every `ByteBuffer` read is bounds checked.
- **Items.** `local_ids`, `categories`, and `attributes` are parallel arrays,
  one entry per item. A local id is the IFC express id. `guids_items` holds
  local ids, not positions; `meshes_items` holds positions in `local_ids`.
- **Attributes.** Each attribute is the JSON text `[name, value, ifcType]`, for
  example `["OverallHeight",2.01,"IFCPOSITIVELENGTHMEASURE"]`. A null IFC
  attribute is not written. `GlobalId` is moved to `guids`; `OwnerHistory`,
  `ObjectPlacement`, `Representation`, and `CompositionType` are excluded by
  the importer.
- **Relations.** Each relation is the JSON text `[name, id, id, ...]`, and
  `relations_items[i]` is the local id it belongs to. Both sides of an IFC
  relation are written (`ContainedInStructure` on the element,
  `ContainsElements` on the storey), and so is every entity-valued attribute
  (`HasProperties`, `Material`, `Units`). Targets can be ids that are not items
  (`Dimensions` points at id 0; `RepresentationMaps` at excluded classes).
- **Spatial structure.** `spatial_structure` is built from the
  `IsDecomposedBy`, `ContainsElements`, `ReferencesElements`, and `IsNestedBy`
  relations, which the reader already reads, so the tree itself is not read.

The BOS that results follows `Ara3D.Ifc.Bos`'s conventions:

| Fragments | BOS |
|---|---|
| each item | an entity: LocalId = local id, GlobalId from `guids`, Name from the `Name` attribute |
| each category string | a category entity, as Ara3D.Ifc.Bos makes one per IFC class |
| `ObjectTypeOf` on a type | the instances' `Type` |
| other attributes | parameters `Ifc:<name>`, grouped by the class |
| `IsDefinedBy` / `HasPropertySets` to a property or quantity set | one parameter per member, named by the member, grouped by the set |
| `ContainedInStructure`, `Decomposes`, `Nests`, `HasAssociations` | relations ContainedIn, MemberOf, ChildOf, HasMaterial |
| any other relation name (`Material`, `Units`, `ReferencedInStructures`) | Entity parameters `Ifc:<name>` |
| the relating side (`ContainsElements`, `DefinesOccurrence`, ...) | nothing: it repeats the related side |

Values keep the type the file states: a JSON string is a String parameter, a
boolean an Int 0 or 1, a number an Int when its IFC type is an integer type
(`IFCINTEGER`, `IFCDIMENSIONCOUNT`, ...) and a Number otherwise, and a list a
String holding its JSON text. A missing value adds no parameter. A relation
target that is not an item is dropped and counted in one diagnostic per
relation name; malformed JSON is reported the same way.

An entity with no GlobalId or no Name gets the empty string, as every BOS
writer does for those two columns.

## What it drops

- The IFC value type name (`IFCLENGTHMEASURE`) beyond choosing Int or Number.
- `metadata`, `guid`, `max_local_id`, `unique_attributes`, `relation_names`,
  and `indexes` (`ModelIndex`, user-defined lookups).
- Classes the importer did not keep. By default That Open's importer skips
  `IfcOpeningElement`, door and window styles, and lining properties, so a
  `.frag` has no Voids or Fills relations and no opening elements.

## Measured on the Duplex fixture

`tests/data/Ara3D.BimOpenSchema.IO.Fragments.Tests/Fixtures/duplex.frag`
(248,676 bytes, SHA-256 `3384ed78…125ba7`), written by
`@thatopen/fragments` 3.4.8 with web-ifc 0.0.77 from `samples/nrc/duplex-base.ifc`,
compared with `samples/public/duplex.bos` (the same IFC through Ara3D.Ifc.Bos):

| | `.frag` read here | `duplex.bos` |
|---|---:|---:|
| Items in the file | 7,156 | |
| Entities (with categories) | 7,186 | 4,721 |
| Parameters | 19,196 | 15,658 |
| GlobalIds | 1,751 | 345 |
| GlobalIds in both | 271 | 271 |
| ContainedIn pairs (storey containment) | 207, all equal | 207 |
| MemberOf pairs (aggregation) | 38, all equal | 38 |

Every IFC class in both has the same count. The 74 GlobalIds only in
`duplex.bos` are openings, door and window styles, and lining properties; the
1,480 only in the `.frag` are property and quantity sets, which Ara3D.Ifc.Bos
keeps as parameters rather than entities.
