// Writes a .meshdump file: the format tests/data/Ara3D.IfcMeshingComparison/Harness/MeshDump.cs reads.
// Header: ASCII MDMP, int32 version 1, int32 JSON length, UTF-8 JSON. Body: for each mesh in order,
// points x 3 float32 then triangles x 3 int32, little-endian. Lengths in metres. An instance matrix is
// 16 floats with the translation at indices 12, 13, 14.
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, dirname } from 'node:path';

export function sourceOf(ifcPath) {
  const data = readFileSync(ifcPath);
  return { fileName: basename(ifcPath), sha256: createHash('sha256').update(data).digest('hex'), bytes: data.length };
}

export function readHeader(path) {
  const data = readFileSync(path);
  if (data.toString('latin1', 0, 4) !== 'MDMP') throw new Error(`${path} is not a mesh dump`);
  const length = data.readInt32LE(8);
  return JSON.parse(data.toString('utf8', 12, 12 + length));
}

/** meshes: [{points: Float32Array, triangles: Int32Array}]; instances: [{entity, mesh, matrix: number[16]}]. */
export function writeDump(path, { mesher, mesherVersion, source, elapsedMs, meshes, instances }) {
  const header = {
    mesher,
    mesherVersion,
    source,
    elapsedMs: Math.round(elapsedMs),
    generatedUtc: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'),
    meshes: meshes.map((m) => ({ points: m.points.length / 3, triangles: m.triangles.length / 3 })),
    instances,
  };
  const json = Buffer.from(JSON.stringify(header), 'utf8');
  const prefix = Buffer.alloc(12);
  prefix.write('MDMP', 0, 'latin1');
  prefix.writeInt32LE(1, 4);
  prefix.writeInt32LE(json.length, 8);
  const parts = [prefix, json];
  for (const m of meshes) {
    parts.push(Buffer.from(m.points.buffer, m.points.byteOffset, m.points.byteLength));
    parts.push(Buffer.from(m.triangles.buffer, m.triangles.byteOffset, m.triangles.byteLength));
  }
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, Buffer.concat(parts));
}
