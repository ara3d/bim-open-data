// Reads a .bos archive in the browser: a zip of Parquet files, one per table.
import JSZip from 'jszip';
import { parquetMetadata, parquetReadObjects, type FileMetaData } from 'hyparquet';
import { compressors } from 'hyparquet-compressors';

export interface Column {
  readonly name: string;
  /** The Parquet logical type when the file states one, else the physical type: `int64`, `uint8`, `float`, `string`. */
  readonly type: string;
}

export interface Table {
  /** The zip entry path, such as `Entities.parquet`. */
  readonly path: string;
  /** The entry's base name without `.parquet`, such as `Entities`. */
  readonly name: string;
  readonly rowCount: number;
  readonly columns: readonly Column[];
  /** Compressed size of the Parquet file inside the archive, in bytes. */
  readonly byteLength: number;
  readonly bytes: ArrayBuffer;
}

export type Row = Record<string, unknown>;

export interface Archive {
  readonly tables: readonly Table[];
}

const baseName = (path: string): string => (path.split('/').pop() ?? path).replace(/\.parquet$/i, '');

const typeOf = (element: FileMetaData['schema'][number]): string => {
  const logical = element.logical_type;
  if (logical?.type === 'INTEGER') return `${logical.isSigned ? 'int' : 'uint'}${logical.bitWidth}`;
  if (logical) return logical.type.toLowerCase();
  if (element.converted_type === 'UTF8') return 'string';
  return (element.type ?? 'group').toLowerCase();
};

const columnsOf = (metadata: FileMetaData): Column[] =>
  metadata.schema.slice(1).filter((element) => !element.num_children).map((element) => ({ name: element.name, type: typeOf(element) }));

/** Opens the archive and reads each table's footer: row count and columns. Row data is decoded on demand. */
export async function openArchive(buffer: ArrayBuffer): Promise<Archive> {
  const zip = await JSZip.loadAsync(buffer);
  const parquet = Object.values(zip.files).filter((file) => !file.dir && file.name.toLowerCase().endsWith('.parquet'));
  if (parquet.length === 0) throw new Error('The archive holds no Parquet tables, so it is not a BOS file.');
  const tables = await Promise.all(parquet.map(async (file): Promise<Table> => {
    const bytes = await file.async('arraybuffer');
    const metadata = parquetMetadata(bytes);
    return { path: file.name, name: baseName(file.name), rowCount: Number(metadata.num_rows), columns: columnsOf(metadata), byteLength: bytes.byteLength, bytes };
  }));
  return { tables: tables.sort((a, b) => a.name.localeCompare(b.name)) };
}

/** Decodes rows `[start, end)` of one table. */
export const readRows = (table: Table, start: number, end: number): Promise<Row[]> =>
  parquetReadObjects({ file: table.bytes, compressors, rowStart: start, rowEnd: Math.min(end, table.rowCount) });

/** Decodes one whole column, for lookups such as the string table. */
export async function readColumn(table: Table, column: string): Promise<unknown[]> {
  const rows = await parquetReadObjects({ file: table.bytes, compressors, columns: [column] });
  return rows.map((row) => row[column]);
}
