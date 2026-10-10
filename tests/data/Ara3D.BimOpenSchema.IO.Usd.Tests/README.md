# Ara3D.BimOpenSchema.IO.Usd.Tests

NUnit tests for [Ara3D.BimOpenSchema.IO.Usd](../../../src/data/Ara3D.BimOpenSchema.IO.Usd),
the BOS to `.usda` writer.

```
dotnet test tests/data/Ara3D.BimOpenSchema.IO.Usd.Tests -c Release
```

- `SampleExportTests` writes `samples/public/duplex.bos` and `schependomlaan.bos` (both
  committed, so no fetch is needed) and checks element, instance, mesh, and material counts
  against counts taken straight from the BOS tables, in the summary and in the text. It prints
  each file's size and write time; the README's measurements come from that output.
- `SyntheticExportTests` builds a five-entity model by hand to cover what the samples lack:
  missing values, a duplicate parameter, an instance with no entity, one with no mesh, a hidden
  one, a rotated and mirrored one, and strings that need escaping.
- `FileWriteTests` makes a write throw partway and checks that the earlier file survives and
  no temporary file is left.
- `UsdaWriterTests` covers number and string formatting and identifier sanitizing.

## The usd-core check

Four tests also open the stage with real USD: `check_usda.py` (copied next to the test
binary) loads it with usd-core, runs every `UsdValidation` validator (the checks
`usdchecker` runs; the usd-core wheel has no `usdchecker` script), and prints counts and
attribute values as JSON for the test to compare.

They use the Python named by the `BOS_USD_PYTHON` environment variable, or `python` on
`PATH`. When that Python cannot import `pxr`, those tests are reported as skipped with the
reason; they never fail for lack of usd-core. To run them:

```
python -m venv %TEMP%\usd-venv
%TEMP%\usd-venv\Scripts\python -m pip install usd-core
set BOS_USD_PYTHON=%TEMP%\usd-venv\Scripts\python.exe
```

Output files go to `%TEMP%\bos-usd-tests`, outside the repository.
