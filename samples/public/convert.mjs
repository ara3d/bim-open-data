#!/usr/bin/env node
// Converts the fetched source IFC files into the committed samples that samples.json lists:
// <name>.bos and <name>.duckdb in samples/public/. One source: the CLI's convert-ifc verb (IFC to
// BOS with geometry, then DuckDB with the text views, as the IFC MCP server's ifc_to_bos builds).
// Several sources: its federate-union verb (Ara3D.BimOpenSchema.Federation), which keeps each
// source as its own document with all its entities and geometry, merging nothing.
//
//   node samples/public/fetch.mjs
//   node samples/public/convert.mjs                        # every sample
//   node samples/public/convert.mjs duplex-federated ...   # only the named samples
//
// Runs from the repository root and passes the sources as relative paths, because the converter
// records each source path in the Documents table and a relative one keeps machine-local folders
// out of the committed files.
import { execFileSync } from "node:child_process";
import { copyFileSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const catalog = JSON.parse(readFileSync(join(here, "samples.json"), "utf8"));
const { sources } = catalog;
const names = process.argv.slice(2);
const unknown = names.filter((name) => !catalog.samples.some((s) => s.name === name));
if (unknown.length > 0) throw new Error(`Not in samples.json: ${unknown.join(", ")}`);
const samples = names.length === 0 ? catalog.samples : catalog.samples.filter((s) => names.includes(s.name));
const project = "tools/building-model-workflows";
const cli = join(root, project, "bin/Release/net8.0-windows/BuildingModel.Workflows.Cli.exe");

const run = (file, args) => execFileSync(file, args, { cwd: root, stdio: "inherit" });
run("dotnet", ["build", project, "-c", "Release", "--nologo", "-v", "q"]);

for (const sample of samples) {
  const inputs = sample.sources.map((name) => `samples/public/sources/${sources[name].file}`);
  const bos = `samples/public/${sample.name}.bos`;
  const duckdb = `samples/public/${sample.name}.duckdb`;
  if (inputs.length === 1) {
    run(cli, ["convert-ifc", inputs[0], bos, duckdb]);
    continue;
  }
  const scratch = mkdtempSync(join(tmpdir(), "bos-union-"));
  try {
    run(cli, ["federate-union", scratch, ...inputs]);
    copyFileSync(join(scratch, "union.bos"), join(root, bos));
    copyFileSync(join(scratch, "union.duckdb"), join(root, duckdb));
    console.log(`${sample.name}: union of ${inputs.length} documents`);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}
