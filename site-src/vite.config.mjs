// Builds the BOS explorer into ../site/explorer/, the folder the Pages workflow deploys.
// The viewer packages are read as TypeScript source from ../deps/bim-open-viewer (filled by
// `node deps.mjs` from deps.json), so nothing needs building there first. Their third-party
// imports resolve from this folder's node_modules.
import { defineConfig } from 'vite';
import { readFileSync } from 'node:fs';
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

export default defineConfig({
  base: './',
  plugins: [sampleArchive],
  resolve: {
    alias: {
      '@bim-open-viewer/core': viewerPackage('core'),
      '@bim-open-viewer/controls': viewerPackage('controls'),
      '@bim-open-viewer/loaders': viewerPackage('loaders'),
    },
    dedupe: ['three', 'jszip', 'hyparquet', 'hyparquet-compressors'],
  },
  build: {
    outDir: '../site/explorer',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
  },
});
