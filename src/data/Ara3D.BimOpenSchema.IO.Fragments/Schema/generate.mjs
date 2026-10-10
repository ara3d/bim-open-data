#!/usr/bin/env node
// Regenerates Fragments_generated.cs from index.fbs, That Open's Fragments 2 schema copied
// verbatim (see ../README.md for the pinned commit). index.fbs declares no namespace, so this
// script prepends one in a temporary copy rather than editing the copy of the schema.
//
//   node generate.mjs <path to flatc 25.2.10>
//
// flatc comes from https://github.com/google/flatbuffers/releases/tag/v25.2.10 (the same version
// as the Google.FlatBuffers package the project references; the generated code checks it).
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const flatc = process.argv[2];
if (!flatc) throw new Error("Usage: node generate.mjs <path to flatc>");

const here = dirname(fileURLToPath(import.meta.url));
const scratch = mkdtempSync(join(tmpdir(), "fragments-schema-"));
try {
  const schema = join(scratch, "Fragments.fbs");
  const source = readFileSync(join(here, "index.fbs"), "utf8");
  writeFileSync(schema, `namespace Ara3D.BimOpenSchema.IO.Fragments.Schema;\n${source}`);
  execFileSync(flatc, ["--csharp", "--gen-onefile", "-o", scratch, schema], { stdio: "inherit" });
  renameSync(join(scratch, "Fragments_generated.cs"), join(here, "Fragments_generated.cs"));
} finally {
  rmSync(scratch, { recursive: true, force: true });
}
