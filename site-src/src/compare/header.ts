// The text of a pane header's statistics line.
import type { GlbEntry } from './manifest';

const count = (value: number): string => value.toLocaleString('en-US');

export const describeEntry = (entry: GlbEntry): string =>
  `${count(entry.triangles)} triangles · ${count(entry.instances)} instances · ${count(entry.elapsedMs)} ms`;
