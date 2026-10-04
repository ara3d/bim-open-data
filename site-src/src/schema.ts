// What the BIM Open Schema specification says a column's numbers mean, so the explorer can show
// text beside them. Taken from src/Ara3D.BimOpenSchema/BimOpenSchema.cs in ara3d/bim-open-schema
// at the commit deps.json pins.
import { readColumn, type Archive, type Table } from './archive.js';

// 'parameter': the Value column of the single Parameters table, whose meaning follows the row's
// descriptor type (an index into Numbers, Entities, Strings, or Points, or the integer itself).
type Reference = 'string' | 'entity' | 'descriptor' | 'document' | 'parameter' | readonly string[];

const parameterType = ['Int', 'Number', 'Entity', 'String', 'Point'];
const relationType = ['PartOf', 'MemberOf', 'ContainedIn', 'HostedBy', 'ChildOf', 'HasLayer', 'HasMaterial', 'ConnectsTo',
  'HasConnector', 'BoundedBy', 'TraverseTo', 'Voids', 'Fills', 'Covers', 'Serves'];
const diagnosticType = ['RevitWarning', 'RevitError', 'ExporterWarning', 'ExporterError', 'ExporterInfo'];

const parameter = (value?: Reference): Record<string, Reference> =>
  ({ Entity: 'entity', Descriptor: 'descriptor', ...(value ? { Value: value } : {}) });

const references: Record<string, Record<string, Reference>> = {
  Entities: { GlobalId: 'string', Document: 'document', Name: 'string', Category: 'entity', Type: 'entity' },
  Documents: { Title: 'string', Path: 'string' },
  Descriptors: { Name: 'string', Units: 'string', Group: 'string', Type: parameterType },
  Relations: { EntityA: 'entity', EntityB: 'entity', RelationType: relationType },
  Diagnostics: { Type: diagnosticType, Document: 'document', Entity: 'entity', Message: 'string' },
  Parameters: parameter('parameter'),
  IntegerParameters: parameter(),
  SingleParameters: parameter(),
  PointParameters: parameter(),
  StringParameters: parameter('string'),
  EntityParameters: parameter('entity'),
  Instances: { InstanceEntityIndex: 'entity' },
};

/** True when the specification gives this table's column a meaning the explorer can show as text. */
export const hasMeaning = (table: string, column: string): boolean => references[table]?.[column] !== undefined;

/** Turns index columns into text: string-table entries, entity and document names, descriptor names, enum names. */
export interface Resolver {
  /** `row` is the whole row, which a Parameters Value needs for its descriptor. */
  readonly describe: (table: string, column: string, value: unknown, row?: Readonly<Record<string, unknown>>) => string | undefined;
}

const at = (list: readonly unknown[] | undefined, value: unknown): unknown => {
  const index = Number(value);
  return list && Number.isInteger(index) && index >= 0 && index < list.length ? list[index] : undefined;
};

const findTable = (archive: Archive, name: string): Table | undefined => archive.tables.find((table) => table.name === name);

/** Reads the lookup columns the archive has; a table it lacks leaves its references unresolved. */
export async function createResolver(archive: Archive): Promise<Resolver> {
  const column = async (name: string, field: string) => {
    const table = findTable(archive, name);
    return table && table.columns.some((c) => c.name === field) ? readColumn(table, field) : undefined;
  };
  const [strings, entityNames, documentTitles, descriptorNames, descriptorTypes, numbers, pointX, pointY, pointZ] = await Promise.all([
    column('Strings', 'Strings'), column('Entities', 'Name'), column('Documents', 'Title'), column('Descriptors', 'Name'),
    column('Descriptors', 'Type'), column('Numbers', 'Numbers'), column('Points', 'X'), column('Points', 'Y'), column('Points', 'Z'),
  ]);
  const text = (value: unknown): string | undefined => {
    const found = at(strings, value);
    return found === undefined ? undefined : String(found);
  };
  const number = (value: unknown): string | undefined => {
    const found = at(numbers, value);
    return found === undefined ? undefined : String(Number(Number(found).toPrecision(7)));
  };
  const point = (value: unknown): string | undefined => {
    const coordinates = [pointX, pointY, pointZ].map((axis) => at(axis, value));
    return coordinates.some((c) => c === undefined) ? undefined : `(${coordinates.map((c) => Number(Number(c).toPrecision(7))).join(', ')})`;
  };
  // Parameter values by ParameterType: Int, Number, Entity, String, Point.
  const parameterValue = (value: unknown, row?: Readonly<Record<string, unknown>>): string | undefined => {
    switch (parameterType[Number(at(descriptorTypes, row?.Descriptor))]) {
      case 'Number': return number(value);
      case 'Entity': return Number(value) < 0 ? 'none' : text(at(entityNames, value));
      case 'String': return text(value);
      case 'Point': return point(value);
      default: return undefined;
    }
  };
  const describe = (table: string, field: string, value: unknown, row?: Readonly<Record<string, unknown>>): string | undefined => {
    const reference = references[table]?.[field];
    if (reference === undefined || value === null || value === undefined) return undefined;
    if (reference === 'parameter') return parameterValue(value, row);
    if (Number(value) < 0) return 'none';
    if (Array.isArray(reference)) return at(reference, value) as string | undefined;
    switch (reference) {
      case 'string': return text(value);
      case 'entity': return text(at(entityNames, value));
      case 'document': return text(at(documentTitles, value));
      case 'descriptor': return text(at(descriptorNames, value));
      default: return undefined;
    }
  };
  return { describe };
}
