// The archive's geometry tables drawn with the BIM Open Viewer's renderer core, loader, and orbit controls.
import { Viewer, sceneBounds } from '@bim-open-viewer/core';
import { OrbitControls } from '@bim-open-viewer/controls';
import { bosToGroups, parseBosGeometry, type BosGeometry } from '@bim-open-viewer/loaders';

export interface ModelView {
  /** Replaces the drawn model; resolves to the number of instances drawn. */
  readonly show: (buffer: ArrayBuffer) => Promise<number>;
  readonly clear: () => void;
}

const geometryTables = ['Instances', 'Meshes', 'VertexBuffer', 'IndexBuffer', 'Materials', 'Transforms'];

/** True when the archive carries every table the loader needs to draw it. */
export const hasGeometry = (tableNames: readonly string[]): boolean => geometryTables.every((name) => tableNames.includes(name));

// Archives written before the Instances table gained InstanceFlags lack the column, and the
// viewer's bosToGroups at the pinned commit reads it unguarded. No flags means nothing is hidden.
const withInstanceFlags = (bos: BosGeometry): BosGeometry =>
  bos.InstanceFlags ? bos : { ...bos, InstanceFlags: new Int32Array(bos.InstanceMeshIndex.length) };

// BOS models are z-up, as Revit and IFC are; the viewer core's camera and lights are y-up.
// Left-multiplies each column-major 4x4 by the rotation (x, y, z) -> (x, z, -y), in place.
const zUpToYUp = (transforms: Float32Array): void => {
  for (let column = 0; column < transforms.length; column += 4) {
    const y = transforms[column + 1];
    transforms[column + 1] = transforms[column + 2];
    transforms[column + 2] = -y;
  }
};

export function createModelView(canvas: HTMLCanvasElement): ModelView {
  const viewer = new Viewer({ background: 0xf4f5f7 });
  viewer.attach(canvas);
  const controls = new OrbitControls(viewer);
  controls.attach({
    addEventListener: (type, listener) => canvas.addEventListener(type, listener as EventListener),
    removeEventListener: (type, listener) => canvas.removeEventListener(type, listener as EventListener),
    get clientHeight() { return canvas.clientHeight; },
    setPointerCapture: (id) => canvas.setPointerCapture(id),
    releasePointerCapture: (id) => canvas.releasePointerCapture(id),
  });

  const resize = () => viewer.resize(canvas.clientWidth, canvas.clientHeight, Math.min(devicePixelRatio, 2));
  new ResizeObserver(resize).observe(canvas);

  const frame = () => {
    resize();
    const bounds = sceneBounds(viewer.scene);
    if (!bounds) return;
    const vertical = (viewer.camera.fov * Math.PI) / 180;
    const horizontal = 2 * Math.atan(Math.tan(vertical / 2) * viewer.camera.aspect);
    controls.model.frame(bounds, Math.min(vertical, horizontal));
    controls.model.dolly(0.7); // the bounding sphere is loose around a flat site; come a little closer
    const radius = Math.hypot(...bounds.max.map((max, axis) => max - bounds.min[axis])) / 2;
    viewer.camera.near = Math.max(radius / 1000, 0.01);
    viewer.camera.far = radius * 20;
    viewer.camera.updateProjectionMatrix();
    controls.update();
  };

  const clear = () => {
    viewer.scene.clear();
    viewer.requestRender();
  };

  const show = async (buffer: ArrayBuffer): Promise<number> => {
    clear();
    const result = bosToGroups(withInstanceFlags(await parseBosGeometry(buffer)), (group) => {
      zUpToYUp(group.transforms);
      viewer.scene.addGroup(group);
    });
    frame();
    return result.instanceCount;
  };

  return { show, clear };
}
