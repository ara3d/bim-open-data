#!/usr/bin/env node
// Downloads the source IFC files that samples.json lists, from their upstream repositories at
// the pinned commits, into samples/public/sources/ (git-ignored), and checks each SHA-256.
// A file already present with the right hash is kept.
//
//   node samples/public/fetch.mjs
//
// No packages: Node 18 or later (for fetch).
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const { sources } = JSON.parse(readFileSync(join(here, "samples.json"), "utf8"));
const folder = join(here, "sources");
mkdirSync(folder, { recursive: true });

const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");
let failures = 0;

for (const [name, source] of Object.entries(sources)) {
  const target = join(folder, source.file);
  if (existsSync(target) && sha256(readFileSync(target)) === source.sha256) {
    console.log(`ok     ${name.padEnd(16)} ${source.file}`);
    continue;
  }
  console.log(`fetch  ${name.padEnd(16)} ${source.url}`);
  const response = await fetch(source.url);
  if (!response.ok) { console.error(`error: HTTP ${response.status} for ${source.url}`); failures++; continue; }
  const bytes = Buffer.from(await response.arrayBuffer());
  const hash = sha256(bytes);
  if (hash !== source.sha256 || bytes.length !== source.bytes) {
    console.error(`error: ${source.file} is ${bytes.length} bytes with SHA-256 ${hash}; samples.json expects ${source.bytes} bytes and ${source.sha256}`);
    failures++;
    continue;
  }
  writeFileSync(`${target}.part`, bytes);
  renameSync(`${target}.part`, target);
  console.log(`ok     ${name.padEnd(16)} ${source.file} (${bytes.length.toLocaleString("en-US")} bytes)`);
}

if (failures > 0) { console.error(`${failures} source file(s) failed`); process.exitCode = 1; }
