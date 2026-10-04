// Headless check of the built site: serves ../site under /bim-open-data/ (the path GitHub Pages
// uses), opens the explorer in Edge, and checks that every bundled sample lists its tables with
// the row counts ../samples/public/samples.json records, shows rows and their credit, and draws
// the model, and that a file chosen through the file input (the geometry-free federated
// DigitalHub) replaces it.
// Fails on any console error, page error, failed request, or HTTP error.
//
//   PLAYWRIGHT_CORE=<path to playwright-core> node scripts/smoke.mjs [--screenshot <png>]
//
// playwright-core is not a dependency of this folder; point PLAYWRIGHT_CORE at any installed copy.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { SAMPLES } from '../sample.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const siteRoot = resolve(here, '../../site');
const samplesFolder = resolve(here, '../../samples/public');
const recorded = JSON.parse(await readFile(join(samplesFolder, 'samples.json'), 'utf8')).samples;
const entitiesOf = (file) => recorded.find((sample) => `${sample.name}.bos` === file).entities.toLocaleString('en-US');
const dropFile = join(samplesFolder, 'digitalhub-federated.bos');
const screenshotArg = process.argv.indexOf('--screenshot');
const screenshot = screenshotArg > 0 ? resolve(process.argv[screenshotArg + 1]) : undefined;
const prefix = '/bim-open-data/';

const playwrightPath = process.env.PLAYWRIGHT_CORE;
const { chromium } = await import(playwrightPath ? pathToFileURL(join(playwrightPath, 'index.mjs')).href : 'playwright-core');

const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.bos': 'application/octet-stream' };
const server = createServer(async (request, response) => {
  const path = decodeURIComponent(new URL(request.url, 'http://x').pathname);
  if (!path.startsWith(prefix)) { response.statusCode = 404; response.end(); return; }
  let file = normalize(join(siteRoot, path.slice(prefix.length)));
  if (path.endsWith('/')) file = join(file, 'index.html');
  if (!file.startsWith(siteRoot) || !existsSync(file)) { response.statusCode = 404; response.end('not found'); return; }
  response.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream');
  response.end(await readFile(file));
});
await new Promise((done) => server.listen(0, '127.0.0.1', done));
const base = `http://127.0.0.1:${server.address().port}${prefix}`;

const problems = [];
const check = (condition, message) => { if (!condition) problems.push(message); console.log(`${condition ? 'ok  ' : 'FAIL'} ${message}`); };

const browser = await chromium.launch({ channel: 'msedge', headless: true, args: ['--enable-unsafe-swiftshader'] });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  page.on('console', (message) => { if (message.type() === 'error') problems.push(`console error: ${message.text()}`); });
  page.on('pageerror', (error) => problems.push(`page error: ${error.message}`));
  page.on('requestfailed', (request) => problems.push(`request failed: ${request.url()} (${request.failure()?.errorText})`));
  page.on('response', (response) => { if (response.status() >= 400) problems.push(`HTTP ${response.status()}: ${response.url()}`); });

  await page.goto(base);
  check(await page.locator('a[href="explorer/"]').count() > 0, 'the landing page links to explorer/');

  const drawn = () => page.waitForFunction(() => /instances drawn|no geometry|not available/.test(document.getElementById('model-note')?.textContent ?? '') && !/Drawing/.test(document.getElementById('model-note')?.textContent ?? ''), null, { timeout: 120000 });
  const entitiesRow = () => page.locator('#tables .table-item', { hasText: /^Entities/ }).textContent();

  await page.goto(`${base}explorer/`);
  await drawn();
  const [first] = SAMPLES;
  const tables = await page.locator('#tables .table-item').evaluateAll((items) => items.map((item) => item.textContent));
  console.log(`     tables: ${tables.join(', ')}`);
  check(await page.locator('#archive-name').textContent() === first.file, `the page opens ${first.file} first`);
  check(tables.length >= 10, `the sample lists ${tables.length} tables`);
  check(await entitiesRow() === `Entities${entitiesOf(first.file)}`, `Entities shows ${entitiesOf(first.file)} rows`);
  check(await page.locator('#rows tbody tr').count() === 50, 'the first 50 rows of Entities show');
  check(await page.locator('#columns li').count() === 6, 'Entities shows its 6 columns');
  check(/CC BY 4\.0/.test(await page.locator('#archive-origin').textContent()), `credit: ${await page.locator('#archive-origin').textContent()}`);
  check(/instances drawn/.test(await page.locator('#model-note').textContent()), `model: ${await page.locator('#model-note').textContent()}`);
  check((await page.locator('#status').getAttribute('data-kind')) !== 'error', `status: ${await page.locator('#status').textContent()}`);
  check(await page.locator('#sample option').count() === SAMPLES.length, `the sample list offers ${SAMPLES.length} buildings`);

  await page.locator('#tables .table-item', { hasText: /^Parameters/ }).click();
  await page.waitForFunction(() => document.getElementById('table-title')?.textContent === 'Parameters');
  await page.waitForFunction(() => document.querySelectorAll('#rows tbody tr').length === 50);
  const resolvedValues = await page.locator('#rows tbody tr td:nth-child(4) .resolved').count();
  check(resolvedValues > 0, `Parameters shows text for ${resolvedValues} of 50 values`);
  await page.locator('#page-next').click();
  await page.waitForFunction(() => /^Rows 50 to 99/.test(document.getElementById('page-label')?.textContent ?? ''));
  check(true, 'Next shows rows 50 to 99');
  await page.locator('#page-previous').click();
  await page.locator('#tables .table-item', { hasText: /^Entities/ }).click();
  await page.waitForFunction(() => document.getElementById('table-title')?.textContent === 'Entities');
  await page.waitForTimeout(500);
  if (screenshot) { await page.setViewportSize({ width: 1280, height: 1100 }); await page.waitForTimeout(300); await page.screenshot({ path: screenshot }); console.log(`     screenshot: ${screenshot}`); }

  for (const sample of SAMPLES.slice(1)) {
    await page.locator('#sample').selectOption(sample.file);
    await page.waitForFunction((file) => document.getElementById('archive-name')?.textContent === file, sample.file, { timeout: 60000 });
    await drawn();
    const note = await page.locator('#model-note').textContent();
    check(await entitiesRow() === `Entities${entitiesOf(sample.file)}`, `${sample.file}: Entities shows ${entitiesOf(sample.file)} rows`);
    check(/instances drawn/.test(note), `${sample.file}: ${note}`);
    check(/converted to BIM Open Schema tables by Ara 3D/.test(await page.locator('#archive-origin').textContent()), `${sample.file}: the credit shows`);
  }

  if (existsSync(dropFile)) {
    await page.locator('#file').setInputFiles(dropFile);
    await page.waitForFunction(() => document.getElementById('archive-name')?.textContent === 'digitalhub-federated.bos', null, { timeout: 60000 });
    await drawn();
    check(/Nothing was uploaded/.test(await page.locator('#archive-origin').textContent()), 'a chosen file says it was read locally');
    check(await entitiesRow() === `Entities${entitiesOf('digitalhub-federated.bos')}`, `the chosen file replaces the sample (${await entitiesRow()})`);
    check(await page.locator('#rows tbody tr').count() > 0, 'the chosen file shows rows');
    check(/no geometry/.test(await page.locator('#model-note').textContent()), `chosen file model: ${await page.locator('#model-note').textContent()}`);
  } else {
    problems.push(`no file to choose: ${dropFile} is missing`);
  }
} catch (error) {
  problems.push(`stopped: ${error.message.split(/\r?\n/)[0]}`);
  const page = browser.contexts()[0]?.pages()[0];
  if (page) problems.push(`status: ${await page.locator('#status').textContent()} | model: ${await page.locator('#model-note').textContent()}`);
} finally {
  await browser.close();
  server.close();
}

for (const problem of problems) console.error(`problem: ${problem}`);
console.log(problems.length === 0 ? 'smoke passed' : `smoke failed with ${problems.length} problem(s)`);
process.exitCode = problems.length === 0 ? 0 : 1;
