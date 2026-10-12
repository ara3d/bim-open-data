// Locks the four panes' cameras: the first pane is linked to each of the others, so a move in any pane reaches all.
import { linkCameraViews } from '@bim-open-viewer/visualization/view-link';
import type { CameraEndpoint } from '@bim-open-viewer/visualization/view-link';

export interface CameraLock {
  readonly setLocked: (locked: boolean) => void;
  readonly locked: () => boolean;
}

/** Starts locked. Locking again starts from the first endpoint's pose. */
export function createCameraLock(endpoints: readonly CameraEndpoint[]): CameraLock {
  let stops: readonly (() => void)[] = [];
  const [leader, ...followers] = endpoints;
  const setLocked = (locked: boolean) => {
    for (const stop of stops) stop();
    stops = locked && leader ? followers.map((follower) => linkCameraViews(leader, follower)) : [];
  };
  setLocked(true);
  return { setLocked, locked: () => stops.length > 0 };
}
