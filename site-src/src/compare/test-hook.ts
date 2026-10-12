// A read-only window for the smoke test: poses and drawn pixels per pane. The page does not use it.
import type { CameraLock } from './camera-link';
import type { Pane } from './pane';
import type { Pose } from './camera-pose';

export interface CompareTestHook {
  readonly poses: () => readonly Pose[];
  readonly inkedPixels: () => readonly number[];
  readonly locked: () => boolean;
}

declare global {
  interface Window { __meshComparison?: CompareTestHook }
}

export function installTestHook(panes: readonly Pane[], lock: CameraLock): void {
  window.__meshComparison = {
    poses: () => panes.map((pane) => pane.endpoint.read()),
    inkedPixels: () => panes.map((pane) => pane.inkedPixels()),
    locked: lock.locked,
  };
}
