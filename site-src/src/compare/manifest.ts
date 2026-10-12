// The mesher comparison's manifest (bench/ifc-mesher/glb/manifest.json): which meshers ran, and per file the GLB each produced.
export interface MesherInfo {
  readonly name: string;
  readonly version: string;
  /** CSS colour that marks the mesher in the page; absent in older manifests. */
  readonly color?: string;
}

export interface GlbEntry {
  /** Relative to the glb/ folder. */
  readonly path: string;
  readonly triangles: number;
  readonly instances: number;
  readonly meshes: number;
  readonly bytes: number;
  readonly elapsedMs: number;
}

export interface FileEntry {
  readonly name: string;
  /** Keyed by mesher name; a mesher with no output for the file is absent. */
  readonly glbs: Readonly<Record<string, GlbEntry>>;
}

export interface Manifest {
  readonly generatedUtc: string;
  readonly meshers: readonly MesherInfo[];
  readonly files: readonly FileEntry[];
}

export const GLB_FOLDER = 'glb/';

export async function fetchManifest(): Promise<Manifest> {
  const response = await fetch(`${GLB_FOLDER}manifest.json`);
  if (!response.ok) throw new Error(`glb/manifest.json: HTTP ${response.status}`);
  return (await response.json()) as Manifest;
}
