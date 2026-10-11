// Meshes one IFC file with web-ifc (the engine under ThatOpen components) and writes a .meshdump.
//
//   node mesh.mjs <model.ifc> <out.meshdump>
//   node mesh.mjs --version
//
// One mesh per geometry express id (web-ifc shares a geometry between placements, as a
// representation map does); each placed geometry becomes an instance with the product's express id
// and web-ifc's flat transformation (16 floats, translation at 12, 13, 14). web-ifc returns metres.
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { performance } from 'node:perf_hooks';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { sourceOf, writeDump } from '../meshdump.mjs';

const require = createRequire(import.meta.url);
const WebIFC = require('web-ifc');
const here = dirname(fileURLToPath(import.meta.url));
const version = JSON.parse(readFileSync(join(here, '..', 'node_modules', 'web-ifc', 'package.json'), 'utf8')).version;

const NAME = 'WebIfc';

// web-ifc bakes a Y-up rotation for three.js into every placement: its (x, y, z) is IFC's (x, z, -y).
// Post-multiplying the placement by the inverse rotation puts the instance back in IFC's Z-up frame,
// where the other meshers' dumps are.
const toIfcFrame = [1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1];
function toZUp(m) {
  const out = new Array(16).fill(0);
  for (let r = 0; r < 4; r++)
    for (let c = 0; c < 4; c++)
      for (let k = 0; k < 4; k++) out[r * 4 + c] += m[r * 4 + k] * toIfcFrame[k * 4 + c];
  return out;
}

async function mesh(ifcPath, outPath) {
  const source = sourceOf(ifcPath);
  const api = new WebIFC.IfcAPI();
  await api.Init();
  const started = performance.now();
  const modelId = api.OpenModel(new Uint8Array(readFileSync(ifcPath)));
  const meshes = [];
  const meshIndexByGeometry = new Map();
  const instances = [];
  api.StreamAllMeshes(modelId, (flatMesh) => {
    const placed = flatMesh.geometries;
    for (let i = 0; i < placed.size(); i++) {
      const pg = placed.get(i);
      let meshIndex = meshIndexByGeometry.get(pg.geometryExpressID);
      if (meshIndex === undefined) {
        const geometry = api.GetGeometry(modelId, pg.geometryExpressID);
        const interleaved = api.GetVertexArray(geometry.GetVertexData(), geometry.GetVertexDataSize());
        const indices = api.GetIndexArray(geometry.GetIndexData(), geometry.GetIndexDataSize());
        const pointCount = interleaved.length / 6; // position xyz, normal xyz
        const points = new Float32Array(pointCount * 3);
        for (let v = 0; v < pointCount; v++) {
          points[v * 3] = interleaved[v * 6];
          points[v * 3 + 1] = interleaved[v * 6 + 1];
          points[v * 3 + 2] = interleaved[v * 6 + 2];
        }
        geometry.delete();
        if (indices.length === 0) {
          meshIndexByGeometry.set(pg.geometryExpressID, -1);
          continue;
        }
        meshIndex = meshes.length;
        meshes.push({ points, triangles: Int32Array.from(indices) });
        meshIndexByGeometry.set(pg.geometryExpressID, meshIndex);
      }
      if (meshIndex < 0) continue;
      instances.push({ entity: flatMesh.expressID, mesh: meshIndex, matrix: toZUp(pg.flatTransformation) });
    }
  });
  const elapsedMs = performance.now() - started;
  api.CloseModel(modelId);
  writeDump(outPath, { mesher: NAME, mesherVersion: version, source, elapsedMs, meshes, instances });
  const triangles = meshes.reduce((n, m) => n + m.triangles.length / 3, 0);
  console.log(`${NAME} ${source.fileName}: ${meshes.length} meshes, ${instances.length} instances, ${triangles} triangles, ${elapsedMs.toFixed(0)} ms -> ${outPath}`);
}

if (process.argv[2] === '--version') {
  console.log(version);
} else {
  await mesh(process.argv[2], process.argv[3]);
}
