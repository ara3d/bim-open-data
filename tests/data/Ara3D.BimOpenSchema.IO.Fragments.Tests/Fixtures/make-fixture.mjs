#!/usr/bin/env node
// Writes duplex.frag, the test fixture, with That Open's own IFC importer, so the reader is
// tested against the bytes That Open's tools write rather than against our idea of them.
//
//   cd tests/data/Ara3D.BimOpenSchema.IO.Fragments.Tests/Fixtures
//   npm install            # the exact versions in package.json; node_modules stays untracked
//   node make-fixture.mjs ../../../../samples/nrc/duplex-base.ifc duplex.frag
//
// The model id is fixed, so the same input and versions give the same bytes (SHA-256 in README).
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname } from "node:path";
import { IfcImporter } from "@thatopen/fragments";

const [input, output] = process.argv.slice(2);
if (!input || !output) throw new Error("Usage: node make-fixture.mjs <input.ifc> <output.frag>");

const require = createRequire(import.meta.url);
const importer = new IfcImporter();
importer.wasm = { path: dirname(require.resolve("web-ifc")) + "/", absolute: true };
const bytes = await importer.process({ id: "duplex-base", bytes: new Uint8Array(readFileSync(input)), raw: false });
writeFileSync(output, bytes);
console.log(`${output}: ${bytes.length} bytes`);
