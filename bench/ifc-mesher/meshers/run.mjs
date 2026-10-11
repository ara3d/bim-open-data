// Runs the external meshers over model files and writes one .meshdump per mesher per model.
//
//   node run.mjs [--out committed|artifacts] [--only WebIfc,IfcLite,IfcOpenShell] [--force] [model.ifc ...]
//
// With no model arguments it meshes the quick comparison files (IfcOpenHouse_IFC4.ifc, example.ifc,
// steelplates.ifc), looked up by name in the repository's data/ folder and in the folders named by
// ARA3D_IFC_TEST_DIRS. --out committed (the default) writes under bench/ifc-mesher/dumps/<mesher>/,
// which is committed for the quick files; --out artifacts writes under artifacts/ifc-mesher/dumps/.
// A dump whose header already has the model's SHA-256 and the tool's version is left alone unless
// --force. Nothing is installed here: run `npm install` in this folder and
// `pip install -r requirements.txt` first; a mesher whose tool is missing is reported and skipped.
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readHeader } from './meshdump.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..', '..');
const quickFiles = ['IfcOpenHouse_IFC4.ifc', 'example.ifc', 'steelplates.ifc'];

const meshers = [
  { name: 'WebIfc', command: ['node', join(here, 'webifc', 'mesh.mjs')] },
  { name: 'IfcLite', command: ['python', join(here, 'ifclite', 'mesh.py')] },
  { name: 'IfcOpenShell', command: ['python', join(here, 'ifcopenshell', 'mesh.py')] },
];

const args = process.argv.slice(2);
const option = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : fallback;
};
const out = option('--out', 'committed');
const only = option('--only', '')?.split(',').filter(Boolean);
const force = args.includes('--force');
const models = args.filter((a, i) => !a.startsWith('--') && args[i - 1] !== '--out' && args[i - 1] !== '--only');

const outRoot = out === 'artifacts'
  ? join(repoRoot, 'artifacts', 'ifc-mesher', 'dumps')
  : join(repoRoot, 'bench', 'ifc-mesher', 'dumps');

function searchFolders() {
  const folders = [join(repoRoot, 'data')];
  for (const f of (process.env.ARA3D_IFC_TEST_DIRS ?? '').split(';')) if (f) folders.push(f);
  return folders;
}

function resolveModel(name) {
  if (existsSync(name)) return resolve(name);
  for (const folder of searchFolders()) {
    const candidate = join(folder, name);
    if (existsSync(candidate)) return candidate;
  }
  return null;
}

function toolVersion(mesher) {
  const r = spawnSync(mesher.command[0], [...mesher.command.slice(1), '--version'], { encoding: 'utf8' });
  return r.status === 0 ? r.stdout.trim() : null;
}

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

const files = (models.length ? models : quickFiles).map((m) => [m, resolveModel(m)]);
const rows = [];
for (const mesher of meshers) {
  if (only?.length && !only.includes(mesher.name)) continue;
  const version = toolVersion(mesher);
  if (!version) {
    console.log(`${mesher.name}: tool not available (${mesher.command.join(' ')} --version failed); skipped`);
    continue;
  }
  for (const [name, path] of files) {
    if (!path) {
      rows.push([mesher.name, name, 'model not found']);
      continue;
    }
    const dump = join(outRoot, mesher.name, basename(path).replace(/\.[^.]+$/, '') + '.meshdump');
    if (!force && existsSync(dump)) {
      const header = readHeader(dump);
      if (header.source?.sha256 === sha256(path) && header.mesherVersion === version) {
        rows.push([mesher.name, name, `current (${version})`]);
        continue;
      }
    }
    const r = spawnSync(mesher.command[0], [...mesher.command.slice(1), path, dump], { encoding: 'utf8' });
    if (r.status === 0) {
      rows.push([mesher.name, name, r.stdout.trim().split('\n').pop()]);
    } else {
      rows.push([mesher.name, name, `failed: ${(r.stderr || r.stdout).trim().split('\n').pop()}`]);
    }
  }
}
for (const [mesher, name, status] of rows) console.log(`${mesher.padEnd(13)} ${name.padEnd(24)} ${status}`);
