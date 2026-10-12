// Frames a model: the pose that shows a bounding box, and the clip planes that suit its size.
import { fitPerspectivePose } from '@bim-open-viewer/visualization/camera';
import type { Viewer } from '@bim-open-viewer/core';
import type { Pose } from './camera-pose';

export interface Box {
  readonly min: readonly [number, number, number];
  readonly max: readonly [number, number, number];
}

const radiusOf = (box: Box): number => Math.hypot(...box.max.map((max, axis) => max - box.min[axis])) / 2;

export function fitPose(viewer: Viewer, box: Box): Pose {
  return fitPerspectivePose(
    { min: [...box.min], max: [...box.max] },
    { verticalFov: (viewer.camera.fov * Math.PI) / 180, aspect: viewer.camera.aspect },
  );
}

export function setClipPlanes(viewer: Viewer, box: Box): void {
  const radius = radiusOf(box);
  viewer.camera.near = Math.max(radius / 1000, 0.01);
  viewer.camera.far = radius * 40;
  viewer.camera.updateProjectionMatrix();
}
