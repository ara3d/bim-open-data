# Ara3D.BimOpenSchema.IO.Bcf.Tests

NUnit tests for [Ara3D.BimOpenSchema.IO.Bcf](../../../src/data/Ara3D.BimOpenSchema.IO.Bcf). Every
written XML file is validated against the buildingSMART BCF 3.0 schemas.

```
dotnet test tests/data/Ara3D.BimOpenSchema.IO.Bcf.Tests -c Release
```

| File | Covers |
|---|---|
| `DuplexBcfTests.cs` | One topic per door type of `samples/public/duplex.bos` (14 doors, 4 types), from a SQL result over the model: with and without geometry, schema validity, selection, camera framing, byte-identical reruns, zip layout |
| `BcfIssueTableTests.cs` | Table rows to issues (grouping, empty cells, the verdict-table preset, errors) and edge cases of the writer |
| `BcfCameraTests.cs` | The perspective camera that frames a box |
| `BcfArchive.cs` | Reads a written container back and validates each file against its schema |

## Schemas

`Schemas/*.xsd` are the BCF 3.0 XML schemas, copied unmodified from
<https://github.com/buildingSMART/BCF-XML/tree/release_3_0/Schemas> at commit
`bc48611d0d7a1587f028a2b69677a1aafd5cd0a8` (fetched 2026-10-10).

Licence: © buildingSMART International Ltd., under the Creative Commons
Attribution-NoDerivatives 4.0 International License
(<http://creativecommons.org/licenses/by-nd/4.0/>), which allows redistributing
unmodified copies with attribution. Do not edit them; fetch a new release instead.
