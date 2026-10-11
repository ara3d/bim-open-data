// What the BIM Open Schema specification says a column's numbers mean, so the explorer can show
// text beside them. Taken from src/Ara3D.BimOpenSchema/BimOpenSchema.cs in ara3d/bim-open-schema
// at the commit deps.json pins. No imports, so scripts/resolver.test.mjs can load it in Node.
//
// An index of -1 means absent (the specification's rule): it shows as `none`, and an entity whose
// name is -1 shows as `no name`, never as strings[-1] or as another row's text.

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

/** The marker shown for an index of -1 (absent). */
export const absent = 'none';

/** The marker shown for an entity that exists but whose name is absent. */
export const unnamed = 'no name';

/** True when the specification gives this table's column a meaning the explorer can show as text. */
export const hasMeaning = (table: string, column: string): boolean => references[table]?.[column] !== undefined;

/** Turns index columns into text: string-table entries, entity and document names, descriptor names, enum names. */
export interface Resolver {
  /** `row` is the whole row, which a Parameters Value needs for its descriptor. */
  readonly describe: (table: string, column: string, value: unknown, row?: Readonly<Record<string, unknown>>) => string | undefined;
}

/** The lookup columns a resolver reads; a column the archive lacks is undefined and leaves its references unresolved. */
export interface Lookups {
  readonly strings?: readonly unknown[];
  readonly entityNames?: readonly unknown[];
  readonly documentTitles?: readonly unknown[];
  readonly descriptorNames?: readonly unknown[];
  readonly descriptorTypes?: readonly unknown[];
  readonly numbers?: readonly unknown[];
  readonly pointX?: readonly unknown[];
  readonly pointY?: readonly unknown[];
  readonly pointZ?: readonly unknown[];
}

const at = (list: readonly unknown[] | undefined, value: unknown): unknown => {
  const index = Number(value);
  return list && Number.isInteger(index) && index >= 0 && index < list.length ? list[index] : undefined;
};

const isAbsent = (value: unknown): boolean => Number(value) < 0;

export function resolverFrom(lookups: Lookups): Resolver {
  const { strings, entityNames, documentTitles, descriptorNames, descriptorTypes, numbers, pointX, pointY, pointZ } = lookups;
  const text = (value: unknown): string | undefined => {
    const found = at(strings, value);
    return found === undefined ? undefined : String(found);
  };
  // The name of the row an index names in a table whose Name (or Title) column is `names`.
  const nameIn = (names: readonly unknown[] | undefined, value: unknown): string | undefined => {
    const name = at(names, value);
    if (name === undefined) return undefined;
    return isAbsent(name) ? unnamed : text(name);
  };
  const number = (value: unknown): string | undefined => {
    const found = at(numbers, value);
    return found === undefined ? undefined : String(Number(Number(found).toPrecision(7)));
  };
  const point = (value: unknown): string | undefined => {
    const coordinates = [pointX, pointY, pointZ].map((axis) => at(axis, value));
    return coordinates.some((c) => c === undefined) ? undefined : `(${coordinates.map((c) => Number(Number(c).toPrecision(7))).join(', ')})`;
  };
  // Parameter values by ParameterType: Int, Number, Entity, String, Point. Only an Int is the value itself.
  const parameterValue = (value: unknown, row?: Readonly<Record<string, unknown>>): string | undefined => {
    const type = parameterType[Number(at(descriptorTypes, row?.Descriptor))];
    if (type === undefined || type === 'Int') return undefined;
    if (isAbsent(value)) return absent;
    switch (type) {
      case 'Number': return number(value);
      case 'Entity': return nameIn(entityNames, value);
      case 'String': return text(value);
      case 'Point': return point(value);
      default: return undefined;
    }
  };
  const describe = (table: string, field: string, value: unknown, row?: Readonly<Record<string, unknown>>): string | undefined => {
    const reference = references[table]?.[field];
    if (reference === undefined || value === null || value === undefined) return undefined;
    if (reference === 'parameter') return parameterValue(value, row);
    if (isAbsent(value)) return absent;
    if (Array.isArray(reference)) return at(reference, value) as string | undefined;
    switch (reference) {
      case 'string': return text(value);
      case 'entity': return nameIn(entityNames, value);
      case 'document': return nameIn(documentTitles, value);
      case 'descriptor': return nameIn(descriptorNames, value);
      default: return undefined;
    }
  };
  return { describe };
}
