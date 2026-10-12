# ifc-to-bos

A command line over `IfcToBosConverter` (`Ara3D.Ifc.Bos`), the call behind the IFC MCP server's `ifc_to_bos` tool: reads an `.ifc` file and writes the `.bos` archive, geometry included, byte for byte what the tool saves.

```
dotnet build tools/ifc-to-bos -c Release --artifacts-path artifacts/ifc-to-bos
artifacts/ifc-to-bos/bin/ifc-to-bos/release/ifc-to-bos.exe samples/nrc/duplex.ifc duplex.bos
```

The first output line is `ifc-to-bos <version>`; the second is a JSON object with the input, output, output size, and the read and write times in milliseconds. Exit code 0 on success, 1 on a failed conversion (message on stderr), 2 on wrong arguments.
