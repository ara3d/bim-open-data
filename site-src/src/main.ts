import './style.css';
import { SAMPLE } from '../sample.mjs';
import { openArchive, readRows, type Archive, type Row, type Table } from './archive.js';
import { createResolver, hasMeaning, type Resolver } from './schema.js';
import { createModelView, hasGeometry, type ModelView } from './model-view.js';

const PAGE_SIZE = 50;

const element = <T extends HTMLElement>(id: string): T => {
  const found = document.getElementById(id);
  if (!found) throw new Error(`Missing element #${id}`);
  return found as T;
};

const make = <K extends keyof HTMLElementTagNameMap>(tag: K, className?: string, text?: string): HTMLElementTagNameMap[K] => {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
};

const count = (value: number) => value.toLocaleString('en-US');
const kilobytes = (bytes: number) => `${count(Math.max(1, Math.round(bytes / 1024)))} KB`;

const status = (text: string, kind: 'info' | 'error' = 'info') => {
  const node = element('status');
  node.textContent = text;
  node.dataset.kind = kind;
};

const formatValue = (value: unknown): string => {
  if (value === null || value === undefined) return 'null';
  if (typeof value === 'number') return Number.isInteger(value) ? String(value) : String(Number(value.toPrecision(7)));
  if (value instanceof Uint8Array) return `${value.byteLength} bytes`;
  if (value instanceof Date) return value.toISOString();
  return String(value);
};

interface State {
  archive: Archive;
  resolver: Resolver;
  table: Table;
  start: number;
}

let state: State | undefined;
let modelView: ModelView | undefined;
let loadNumber = 0;

const showText = () => element<HTMLInputElement>('show-text').checked;

function renderTableList(archive: Archive) {
  const list = element('tables');
  list.replaceChildren(...archive.tables.map((table) => {
    const button = make('button', 'table-item');
    button.type = 'button';
    button.dataset.table = table.name;
    button.setAttribute('aria-pressed', String(state?.table === table));
    button.append(make('span', 'table-name', table.name), make('span', 'table-count', count(table.rowCount)));
    button.title = `${table.columns.length} columns, ${kilobytes(table.byteLength)} compressed`;
    button.onclick = () => void selectTable(table);
    return button;
  }));
  const rows = archive.tables.reduce((sum, table) => sum + table.rowCount, 0);
  element('tables-summary').textContent = `${archive.tables.length} tables, ${count(rows)} rows`;
}

function renderColumns(table: Table) {
  element('table-title').textContent = table.name;
  element('table-meta').textContent = `${count(table.rowCount)} rows · ${table.columns.length} columns · ${kilobytes(table.byteLength)} in the archive`;
  element('columns').replaceChildren(...table.columns.map((column) => {
    const item = make('li');
    item.append(make('code', undefined, column.name), make('span', 'column-type', column.type));
    return item;
  }));
}

function cell(table: Table, column: string, value: unknown, resolver: Resolver): HTMLTableCellElement {
  const td = make('td');
  const raw = formatValue(value);
  const text = showText() ? resolver.describe(table.name, column, value) : undefined;
  if (text === undefined) {
    td.textContent = raw;
    if (value === null || value === undefined) td.className = 'null';
  } else {
    td.append(make('span', 'resolved', text === '' ? '""' : text), make('span', 'raw', raw));
  }
  return td;
}

function renderRows(table: Table, rows: Row[], start: number, resolver: Resolver) {
  const head = make('tr');
  head.append(make('th', 'row-number', '#'), ...table.columns.map((column) => {
    const th = make('th', undefined, column.name);
    if (showText() && hasMeaning(table.name, column.name)) th.classList.add('meaningful');
    return th;
  }));
  const body = rows.map((row, offset) => {
    const tr = make('tr');
    tr.append(make('td', 'row-number', String(start + offset)), ...table.columns.map((column) => cell(table, column.name, row[column.name], resolver)));
    return tr;
  });
  const thead = make('thead');
  thead.append(head);
  const tbody = make('tbody');
  tbody.append(...body);
  element('rows').replaceChildren(thead, tbody);
  const end = start + rows.length;
  element('page-label').textContent = table.rowCount === 0 ? 'No rows' : `Rows ${count(start)} to ${count(end - 1)} of ${count(table.rowCount)}`;
  element<HTMLButtonElement>('page-previous').disabled = start === 0;
  element<HTMLButtonElement>('page-next').disabled = end >= table.rowCount;
}

async function showPage(start: number) {
  if (!state) return;
  const { table, resolver } = state;
  state.start = start;
  const rows = await readRows(table, start, start + PAGE_SIZE);
  if (state.table === table && state.start === start) renderRows(table, rows, start, resolver);
}

async function selectTable(table: Table) {
  if (!state) return;
  state.table = table;
  for (const button of element('tables').querySelectorAll<HTMLButtonElement>('button'))
    button.setAttribute('aria-pressed', String(button.dataset.table === table.name));
  renderColumns(table);
  await showPage(0);
}

const preferredTable = (archive: Archive): Table =>
  archive.tables.find((table) => table.name === 'Entities') ?? archive.tables[0];

async function showModel(buffer: ArrayBuffer, archive: Archive, number: number) {
  const note = element('model-note');
  const names = archive.tables.map((table) => table.name);
  if (!hasGeometry(names)) {
    modelView?.clear();
    note.textContent = 'This archive has no geometry tables, so there is nothing to draw.';
    return;
  }
  if (!modelView) {
    note.textContent = 'WebGL is not available in this browser, so the model cannot be drawn.';
    return;
  }
  note.textContent = 'Drawing the model…';
  const instances = await modelView.show(buffer);
  if (number === loadNumber) note.textContent = `${count(instances)} instances drawn. Drag to orbit, right-drag to pan, scroll to zoom.`;
}

async function load(name: string, source: () => Promise<ArrayBuffer>, origin: string) {
  const number = ++loadNumber;
  status(`Reading ${name}…`);
  try {
    const buffer = await source();
    const archive = await openArchive(buffer);
    const resolver = await createResolver(archive);
    if (number !== loadNumber) return;
    state = { archive, resolver, table: preferredTable(archive), start: 0 };
    element('archive-name').textContent = name;
    element('archive-origin').innerHTML = origin;
    renderTableList(archive);
    await selectTable(state.table);
    status(`${name}: ${kilobytes(buffer.byteLength)}, read in this browser.`);
    await showModel(buffer, archive, number);
  } catch (error) {
    if (number === loadNumber) status(`Could not read ${name}: ${error instanceof Error ? error.message : String(error)}`, 'error');
  }
}

const loadSample = () => load(
  SAMPLE.file,
  async () => {
    const response = await fetch(`samples/${encodeURIComponent(SAMPLE.file)}`);
    if (!response.ok) throw new Error(`the sample could not be fetched (HTTP ${response.status})`);
    return response.arrayBuffer();
  },
  `${SAMPLE.title}, from <a href="${SAMPLE.source}">bim-open-schema/examples</a>, derived from Autodesk's Revit basic sample project, bundled with this page.`,
);

const loadFile = (file: File) => load(file.name, () => file.arrayBuffer(), 'Your file, read on this device. Nothing was uploaded.');

function wireInputs() {
  const input = element<HTMLInputElement>('file');
  input.onchange = () => { if (input.files?.[0]) void loadFile(input.files[0]); input.value = ''; };
  element<HTMLButtonElement>('load-sample').onclick = () => void loadSample();
  element<HTMLButtonElement>('page-previous').onclick = () => void showPage(Math.max(0, (state?.start ?? 0) - PAGE_SIZE));
  element<HTMLButtonElement>('page-next').onclick = () => void showPage((state?.start ?? 0) + PAGE_SIZE);
  element<HTMLInputElement>('show-text').onchange = () => void showPage(state?.start ?? 0);

  const drop = element('drop');
  let depth = 0;
  const hasFiles = (event: DragEvent) => event.dataTransfer?.types.includes('Files') ?? false;
  window.addEventListener('dragenter', (event) => { if (hasFiles(event)) { depth++; drop.dataset.active = 'true'; } });
  window.addEventListener('dragleave', () => { depth = Math.max(0, depth - 1); if (depth === 0) delete drop.dataset.active; });
  window.addEventListener('dragover', (event) => { if (hasFiles(event)) event.preventDefault(); });
  window.addEventListener('drop', (event) => {
    event.preventDefault();
    depth = 0;
    delete drop.dataset.active;
    const file = event.dataTransfer?.files[0];
    if (file) void loadFile(file);
  });
}

try {
  modelView = createModelView(element<HTMLCanvasElement>('model'));
} catch {
  modelView = undefined;
}
wireInputs();
void loadSample();
