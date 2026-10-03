// Builds the BOS explorer into ../site/explorer/, the folder the Pages workflow deploys.
// The viewer packages are read as TypeScript source from ../deps/bim-open-viewer (filled by
// `node deps.mjs` from deps.json), so nothing needs building there first. Their third-party
// imports resolve from this folder's node_modules.
import { defineConfig } from 'vite';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { SAMPLE } from './sample.mjs';

const here = (path) => fileURLToPath(new URL(path, import.meta.url));
const viewerPackage = (name) => here(`../deps/bim-open-viewer/packages/${name}/src/index.ts`);
const sampleSource = here(`../deps/bim-open-schema/examples/${SAMPLE.file}`);
const samplePath = `samples/${SAMPLE.file}`;

// The sample archive is copied from the schema repository at build time rather than committed.
const sampleArchive = {
  name: 'bos-sample-archive',
  configureServer(server) {
    server.middlewares.use(`/${samplePath}`, (_request, response) => {
      response.setHeader('Content-Type', 'application/octet-stream');
      response.end(readFileSync(sampleSource));
    });
    // The page links the landing page's mark as ../assets/; in development that is /assets/.
    server.middlewares.use('/assets/data-mark.svg', (_request, response) => {
      response.setHeader('Content-Type', 'image/svg+xml');
      response.end(readFileSync(here('../site/assets/data-mark.svg')));
    });
  },
  generateBundle() {
    this.emitFile({ type: 'asset', fileName: samplePath, source: readFileSync(sampleSource) });
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
