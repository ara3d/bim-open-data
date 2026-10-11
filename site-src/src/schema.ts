// Reads the columns the explorer resolves indices against; what they mean is in resolver.ts.
import { readColumn, type Archive, type Table } from './archive.js';
import { resolverFrom, type Resolver } from './resolver.js';

export { hasMeaning, type Resolver } from './resolver.js';

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
  return resolverFrom({ strings, entityNames, documentTitles, descriptorNames, descriptorTypes, numbers, pointX, pointY, pointZ });
}
