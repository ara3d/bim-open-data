# bos-to-usd

A command line over `Ara3D.BimOpenSchema.IO.Usd`: reads a `.bos` file and writes the OpenUSD text stage.

```
dotnet build tools/bos-to-usd -c Release --artifacts-path artifacts/bos-to-usd
artifacts/bos-to-usd/bin/bos-to-usd/release/bos-to-usd.exe samples/public/duplex.bos duplex.usda
```

It prints a JSON object: input, output, output size, read and write times, and the `UsdExportSummary`
(counts of materials, prototypes, entities, instances, properties, and what was left out).

The `.usda` is large (Schependomlaan: 56.8 MB). Convert it to `.usdc` and `.usdz` with usd-core
(`pip install usd-core`); the `usd-web-viewer` repository has `tools/usd_to_usdz.py` for that step and
`tools/convert_models.py` to run both steps over several models. Measured 2026-10-10:

| Model | .bos | .usda | .usdc / .usdz | Write |
|---|---|---|---|---|
| duplex | 0.10 MB | 4.5 MB | 0.74 MB | 0.9 s |
| schependomlaan | 1.11 MB | 56.8 MB | 7.24 MB | 0.8 s |
| digitalhub-federated | 4.85 MB | 127.4 MB | 27.0 MB | 1.9 s |
