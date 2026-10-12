// Headless check of the built mesher comparison page (../site/explorer/compare.html, served from ../site): four panes draw the
// first file from glb/manifest.json, the selector switches files, a drag in one pane moves all four cameras
// while the lock is on and only that pane when it is off. Fails on any console error or failed request.
//
//   PLAYWRIGHT_CORE=<path to playwright-core> node scripts/compare-smoke.mjs
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(fileURLToPath(new URL('.', import.meta.url)), '../../site');
const playwrightPath = process.env.PLAYWRIGHT_CORE;
const { chromium } = await import(playwrightPath ? pathToFileURL(join(playwrightPath, 'index.mjs')).href : 'playwright-core');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.glb': 'model/gltf-binary' };
const server = createServer(async (request, response) => {
  const file = normalize(join(root, decodeURIComponent(new URL(request.url, 'http://x').pathname)));
  if (!file.startsWith(root) || !existsSync(file) || file === root) { response.statusCode = 404; response.end('not found'); return; }
  response.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream');
  response.end(await readFile(file));
});
await new Promise((done) => server.listen(0, '127.0.0.1', done));
const base = `http://127.0.0.1:${server.address().port}/`;
const manifest = JSON.parse(await readFile(join(root, 'explorer/glb/manifest.json'), 'utf8'));

const problems = [];
const check = (condition, message) => { if (!condition) problems.push(message); console.log(`${condition ? 'ok  ' : 'FAIL'} ${message}`); };
const moved = (a, b) => a.some((value, i) => Math.abs(value - b[i]) > 1e-6);
const samePosition = (poses) => poses.every((pose) => !moved(pose.position, poses[0].position));

const browser = await chromium.launch({ channel: 'msedge', headless: true, args: ['--enable-unsafe-swiftshader'] });
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  page.on('console', (message) => { if (message.type() === 'error') problems.push(`console error: ${message.text()}`); });
  page.on('pageerror', (error) => problems.push(`page error: ${error.message}`));
  page.on('requestfailed', (request) => problems.push(`request failed: ${request.url()}`));
  page.on('response', (response) => { if (response.status() >= 400) problems.push(`HTTP ${response.status()}: ${response.url()}`); });

  await page.goto(`${base}explorer/compare.html`);
  const settled = () => page.waitForFunction((file) => document.body.dataset.file === file && document.querySelectorAll('.pane[data-state="loading"]').length === 0, null, { timeout: 120000 });
  check(await page.locator('canvas.pane-canvas').count() === 4, 'four canvases');
  check(await page.locator('#file option').count() === manifest.files.length, `the selector lists ${manifest.files.length} files`);

  for (const file of manifest.files) {
    await page.locator('#file').selectOption(file.name);
    await page.waitForFunction((name) => document.body.dataset.file === name && document.querySelectorAll('.pane[data-state="loading"]').length === 0, file.name, { timeout: 120000 });
    const ink = await page.evaluate(() => window.__meshComparison.inkedPixels());
    for (const [index, mesher] of manifest.meshers.entries()) {
      const pane = page.locator(`.pane[data-mesher="${mesher.name}"]`);
      if (file.glbs[mesher.name]) {
        check(await pane.getAttribute('data-state') === 'ready' && ink[index] > 500, `${file.name} / ${mesher.name}: drawn (${ink[index]} pixels)`);
        check((await pane.locator('.pane-stats').textContent()).includes(file.glbs[mesher.name].triangles.toLocaleString('en-US')), `${file.name} / ${mesher.name}: header shows triangles`);
      } else check(/no output/.test(await pane.locator('.pane-message').textContent()), `${file.name} / ${mesher.name}: no output`);
    }
    check(samePosition(await page.evaluate(() => window.__meshComparison.poses())), `${file.name}: the four cameras start at one pose`);
  }

  await page.locator('#file').selectOption(manifest.files[0].name);
  await settled().catch(() => {});
  const box = await page.locator('.pane').nth(1).boundingBox();
  const drag = async (pane, dx) => {
    const area = await page.locator('.pane').nth(pane).boundingBox();
    await page.mouse.move(area.x + area.width / 2, area.y + area.height / 2 + 20);
    await page.mouse.down();
    await page.mouse.move(area.x + area.width / 2 + dx, area.y + area.height / 2 + 40, { steps: 5 });
    await page.mouse.up();
  };
  const before = await page.evaluate(() => window.__meshComparison.poses());
  await drag(1, 80);
  const locked = await page.evaluate(() => window.__meshComparison.poses());
  check(box !== null && moved(locked[1].position, before[1].position), 'dragging pane 2 moves its camera');
  check(samePosition(locked), 'the other three cameras followed');
  check(locked.every((pose) => !moved(pose.target, locked[0].target)), 'targets match');

  await page.locator('#lock').uncheck();
  await drag(2, -80);
  const free = await page.evaluate(() => window.__meshComparison.poses());
  check(moved(free[2].position, locked[2].position) && !moved(free[0].position, locked[0].position), 'with the lock off only the dragged pane moves');
} catch (error) {
  problems.push(`stopped: ${error.message.split(/\r?\n/)[0]}`);
} finally {
  await browser.close();
  server.close();
}
for (const problem of problems) console.error(`problem: ${problem}`);
console.log(problems.length === 0 ? 'compare smoke passed' : `compare smoke failed with ${problems.length} problem(s)`);
process.exitCode = problems.length === 0 ? 0 : 1;
