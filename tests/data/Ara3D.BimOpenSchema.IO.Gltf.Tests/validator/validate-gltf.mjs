// Runs the Khronos glTF validator over .glb and .gltf files and exits non-zero on any error.
//
//   npm ci                                  (once, in this folder; or npm install)
//   node validate-gltf.mjs <file-or-folder>...
//
// A folder argument validates every .glb in it. Prints one line per file with the issue counts
// by severity, then each error and warning. Exit codes: 0 no errors, 1 an error in some file,
// 2 no files found.
import fs from 'node:fs';
import path from 'node:path';
import validator from 'gltf-validator';

const severities = ['error', 'warning', 'info', 'hint'];

const files = process.argv.slice(2).flatMap(arg =>
  fs.statSync(arg).isDirectory()
    ? fs.readdirSync(arg).filter(f => f.toLowerCase().endsWith('.glb')).sort().map(f => path.join(arg, f))
    : [arg]);

if (files.length === 0) {
  console.error('usage: node validate-gltf.mjs <file-or-folder>...  (no .glb files found)');
  process.exit(2);
}

let failed = 0;
for (const file of files) {
  const report = await validator.validateBytes(new Uint8Array(fs.readFileSync(file)), { maxIssues: 0 });
  const i = report.issues;
  console.log(`${file}: errors ${i.numErrors}, warnings ${i.numWarnings}, infos ${i.numInfos}, hints ${i.numHints} ` +
    `(validator ${report.validatorVersion})`);
  for (const m of i.messages.filter(m => m.severity <= 1))
    console.log(`  ${severities[m.severity]} ${m.code} at ${m.pointer ?? m.offset ?? ''}: ${m.message}`);
  if (i.numErrors > 0) failed++;
}

console.log(`${files.length} file(s), ${failed} with errors`);
process.exit(failed > 0 ? 1 : 0);
