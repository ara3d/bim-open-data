// Builds the BOS explorer into ../site/explorer/, the folder the Pages workflow deploys.
// The viewer packages are read as TypeScript source from ../deps/bim-open-viewer (filled by
// `node deps.mjs` from deps.json), so nothing needs building there first. Their third-party
// imports resolve from this folder's node_modules.
import { defineConfig } from 'vite';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SAMPLES, SAMPLE_FOLDER } from './sample.mjs';

const here = (path) => fileURLToPath(new URL(path, import.meta.url));
const viewerPackage = (name) => here(`../deps/bim-open-viewer/packages/${name}/src/index.ts`);
const sampleSource = (file) => here(`${SAMPLE_FOLDER}${file}`);

// The sample archives are copied from ../samples/public/ at build time, so each is committed once.
const sampleArchive = {
  name: 'bos-sample-archives',
  configureServer(server) {
    for (const { file } of SAMPLES)
      server.middlewares.use(`/samples/${file}`, (_request, response) => {
        response.setHeader('Content-Type', 'application/octet-stream');
        response.end(readFileSync(sampleSource(file)));
      });
    // The page links the landing page's mark as ../assets/; in development that is /assets/.
    server.middlewares.use('/assets/data-mark.svg', (_request, response) => {
      response.setHeader('Content-Type', 'image/svg+xml');
      response.end(readFileSync(here('../site/assets/data-mark.svg')));
    });
  },
  generateBundle() {
    for (const { file } of SAMPLES)
      this.emitFile({ type: 'asset', fileName: `samples/${file}`, source: readFileSync(sampleSource(file)) });
  },
};

// The mesher comparison's GLBs and manifest (bench/ifc-mesher/glb/, or MESHER_GLB_DIR) are served under /glb/
// in development and copied into the build under glb/, like the sample archives.
const glbRoot = process.env.MESHER_GLB_DIR ?? here('../bench/ifc-mesher/glb');
const filesUnder = (folder, prefix = '') => readdirSync(folder, { withFileTypes: true })
  .flatMap((entry) => entry.isDirectory() ? filesUnder(join(folder, entry.name), `${prefix}${entry.name}/`) : [`${prefix}${entry.name}`]);

const mesherGlbs = {
  name: 'mesher-glbs',
  configureServer(server) {
    server.middlewares.use('/glb/', (request, response, next) => {
      const file = normalize(join(glbRoot, decodeURIComponent(request.url.split('?')[0])));
      if (!file.startsWith(normalize(glbRoot)) || !existsSync(file) || !statSync(file).isFile()) return next();
      response.setHeader('Content-Type', file.endsWith('.json') ? 'application/json' : 'model/gltf-binary');
      response.end(readFileSync(file));
    });
  },
  generateBundle() {
    if (!existsSync(glbRoot)) return;
    for (const name of filesUnder(glbRoot))
      this.emitFile({ type: 'asset', fileName: `glb/${name}`, source: readFileSync(join(glbRoot, name)) });
  },
};

export default defineConfig({
  base: './',
  plugins: [sampleArchive, mesherGlbs],
  resolve: {
    alias: {
      '@bim-open-viewer/core': viewerPackage('core'),
      '@bim-open-viewer/controls': viewerPackage('controls'),
      '@bim-open-viewer/loaders': viewerPackage('loaders'),
      '@bim-open-viewer/visualization/view-link': here('../deps/bim-open-viewer/packages/visualization/src/view-link.ts'),
      '@bim-open-viewer/visualization/contracts': here('../deps/bim-open-viewer/packages/visualization/src/contracts.ts'),
      '@bim-open-viewer/visualization/camera': here('../deps/bim-open-viewer/packages/visualization/src/camera.ts'),
    },
    dedupe: ['three', 'jszip', 'hyparquet', 'hyparquet-compressors', 'brotli-dec-wasm'],
  },
  build: {
    outDir: '../site/explorer',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
    rollupOptions: { input: { index: here('index.html'), compare: here('compare.html') } },
  },
});
