import './style.css';
import { createCameraLock } from './camera-link';
import { fetchManifest, type Manifest } from './manifest';
import { createPane, type Pane } from './pane';
import { installTestHook } from './test-hook';

const element = <T extends HTMLElement>(id: string): T => {
  const found = document.getElementById(id);
  if (!found) throw new Error(`Missing element #${id}`);
  return found as T;
};

/** Every pane draws its own GLB of the file, then all take the pose that frames the first one that drew. */
async function showFile(panes: readonly Pane[], manifest: Manifest, name: string): Promise<void> {
  const file = manifest.files.find((candidate) => candidate.name === name);
  await Promise.all(panes.map((pane) => pane.show(file?.glbs[pane.mesher.name])));
  const pose = panes.map((pane) => pane.fitted()).find((candidate) => candidate !== undefined);
  if (pose) for (const pane of panes) pane.endpoint.write(pose);
  document.body.dataset.file = name;
}

async function start(): Promise<void> {
  const manifest = await fetchManifest();
  const panes = manifest.meshers.map(createPane);
  element('panes').append(...panes.map((pane) => pane.element));
  const lock = createCameraLock(panes.map((pane) => pane.endpoint));
  installTestHook(panes, lock);

  const selector = element<HTMLSelectElement>('file');
  selector.replaceChildren(...manifest.files.map((file) => new Option(file.name, file.name)));
  selector.addEventListener('change', () => void showFile(panes, manifest, selector.value));

  const toggle = element<HTMLInputElement>('lock');
  toggle.addEventListener('change', () => lock.setLocked(toggle.checked));

  element('generated').textContent = `Generated ${manifest.generatedUtc}`;
  if (manifest.files[0]) await showFile(panes, manifest, manifest.files[0].name);
}

start().catch((error: Error) => {
  element('status').textContent = `Could not start: ${error.message}`;
});
