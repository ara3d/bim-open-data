// A camera pose as a value, and the adapter that lets the viewer package's link function drive an OrbitControls.
import { Vector3 } from 'three';
import type { Viewer } from '@bim-open-viewer/core';
import type { OrbitControls } from '@bim-open-viewer/controls';
import type { CameraEndpoint } from '@bim-open-viewer/visualization/view-link';
import type { CameraState } from '@bim-open-viewer/visualization/contracts';

export type Pose = CameraState;

const toTuple = (v: Vector3): [number, number, number] => [v.x, v.y, v.z];

export function cameraEndpoint(viewer: Viewer, controls: OrbitControls): CameraEndpoint {
  return {
    read: () => ({
      position: toTuple(viewer.camera.position),
      target: toTuple(controls.model.target),
      up: toTuple(viewer.camera.up),
      zoom: viewer.camera.zoom,
      projection: 'perspective',
    }),
    write(pose) {
      viewer.camera.up.set(...pose.up);
      controls.model.setPose(new Vector3(...pose.position), new Vector3(...pose.target));
      controls.update();
    },
    subscribe: (listener) => controls.subscribe(listener),
  };
}
