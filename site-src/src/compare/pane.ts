// One comparison pane: a header from the manifest over a canvas holding one Viewer, which draws one mesher's GLB.
import { Viewer, sceneBounds } from '@bim-open-viewer/core';
import { OrbitControls } from '@bim-open-viewer/controls';
import { loadGlb } from '@bim-open-viewer/loaders';
import type { CameraEndpoint } from '@bim-open-viewer/visualization/view-link';
import { cameraEndpoint, type Pose } from './camera-pose';
import { fitPose, setClipPlanes } from './fit';
import { GLB_FOLDER, type GlbEntry, type MesherInfo } from './manifest';
import { describeEntry } from './header';
import { countInkedPixels } from './pixels';

export interface Pane {
  readonly mesher: MesherInfo;
  readonly element: HTMLElement;
  readonly endpoint: CameraEndpoint;
  /** Shows the mesher's output for a file, or "no output" when `entry` is undefined. */
  readonly show: (entry: GlbEntry | undefined) => Promise<void>;
  /** The pose that frames what the pane drew, or undefined when it drew nothing. */
  readonly fitted: () => Pose | undefined;
  /** Renders now and counts the pixels that differ from the corner pixel (a test hook). */
  readonly inkedPixels: () => number;
}

const make = <K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text = ''): HTMLElementTagNameMap[K] => {
  const node = document.createElement(tag);
  node.className = className;
  node.textContent = text;
  return node;
};

const backgroundColour = (): number => (matchMedia('(prefers-color-scheme: dark)').matches ? 0x1b1e24 : 0xf4f5f7);

export function createPane(mesher: MesherInfo): Pane {
  const element = make('section', 'pane');
  const title = make('div', 'pane-title');
  title.append(make('strong', 'pane-mesher', mesher.name), make('span', 'pane-version', mesher.version));
  const stats = make('div', 'pane-stats');
  const head = make('header', 'pane-head');
  head.append(title, stats);
  const canvas = make('canvas', 'pane-canvas');
  const message = make('div', 'pane-message');
  element.dataset.mesher = mesher.name;
  if (mesher.color) element.style.setProperty('--mesher', mesher.color);
  element.append(head, canvas, message);

  const viewer = new Viewer({ background: backgroundColour() });
  viewer.attach(canvas);
  const controls = new OrbitControls(viewer);
  controls.attach({
    addEventListener: (type, listener) => canvas.addEventListener(type, listener as EventListener),
    removeEventListener: (type, listener) => canvas.removeEventListener(type, listener as EventListener),
    get clientHeight() { return canvas.clientHeight; },
    style: canvas.style,
    setPointerCapture: (id) => canvas.setPointerCapture(id),
    releasePointerCapture: (id) => canvas.releasePointerCapture(id),
  });
  const resize = () => viewer.resize(canvas.clientWidth, canvas.clientHeight, Math.min(devicePixelRatio, 2));
  new ResizeObserver(resize).observe(canvas);

  let pose: Pose | undefined;
  const show = async (entry: GlbEntry | undefined) => {
    viewer.scene.clear();
    pose = undefined;
    stats.textContent = entry ? describeEntry(entry) : '';
    element.dataset.state = entry ? 'loading' : 'empty';
    message.textContent = entry ? 'Loading…' : 'no output';
    if (entry) {
      try {
        await loadGlb(`${GLB_FOLDER}${entry.path}`, viewer.scene);
        resize();
        const bounds = sceneBounds(viewer.scene);
        if (bounds) { setClipPlanes(viewer, bounds); pose = fitPose(viewer, bounds); }
        element.dataset.state = bounds ? 'ready' : 'empty';
        message.textContent = bounds ? '' : 'no geometry';
      } catch (error) {
        element.dataset.state = 'error';
        message.textContent = `could not load: ${(error as Error).message}`;
      }
    }
    viewer.requestRender();
  };

  const inkedPixels = (): number => {
    resize();
    viewer.renderFrame();
    return countInkedPixels(canvas);
  };

  return { mesher, element, endpoint: cameraEndpoint(viewer, controls), show, fitted: () => pose, inkedPixels };
}
