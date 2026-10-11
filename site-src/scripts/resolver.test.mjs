// What the explorer shows beside an index, for "Show what the indices point to". Runs in Node:
//   node --experimental-strip-types --test scripts/resolver.test.mjs   (npm test)
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { absent, resolverFrom, unnamed } from '../src/resolver.ts';

// Entity 0 has the name "Wall", entity 1 has none (-1); descriptor 0 is a Number, 1 a String,
// 2 an Entity, 3 a Point, 4 an Int.
const { describe } = resolverFrom({
  strings: ['Wall', '', 'Area', 'Doc'],
  entityNames: [0, -1],
  documentTitles: [3],
  descriptorNames: [2, 2, 2, 2, 2],
  descriptorTypes: [1, 3, 2, 4, 0],
  numbers: [12.5],
  pointX: [1], pointY: [2], pointZ: [3],
});
const value = (descriptor, v) => describe('Parameters', 'Value', v, { Descriptor: descriptor });

test('an index of -1 shows as absent in every index column, never as strings[-1]', () => {
  for (const [table, column] of [['Entities', 'GlobalId'], ['Entities', 'Name'], ['Entities', 'Category'], ['Entities', 'Document'],
    ['Documents', 'Title'], ['Descriptors', 'Units'], ['Relations', 'EntityA'], ['Diagnostics', 'Message']])
    assert.equal(describe(table, column, -1), absent, `${table}.${column}`);
  for (const descriptor of [0, 1, 2, 3]) assert.equal(value(descriptor, -1), absent, `parameter type ${descriptor}`);
});

test('an entity whose name is -1 shows as unnamed, not as another string', () => {
  assert.equal(describe('Relations', 'EntityB', 1), unnamed);
  assert.equal(value(2, 1), unnamed);
  assert.equal(describe('Relations', 'EntityB', 0), 'Wall');
});

test('a stored empty string is shown as the empty string, and an Int value as itself', () => {
  assert.equal(value(1, 1), '');
  assert.equal(describe('Entities', 'GlobalId', 1), '');
  assert.equal(value(4, -1), undefined, 'an Int is its own value: -1 is not an index');
});

test('values resolve through their pools', () => {
  assert.equal(value(0, 0), '12.5');
  assert.equal(value(3, 0), '(1, 2, 3)');
  assert.equal(describe('Entities', 'Document', 0), 'Doc');
  assert.equal(describe('Relations', 'RelationType', 2), 'ContainedIn');
});

test('an index past the end of its table resolves to nothing', () => {
  assert.equal(describe('Entities', 'Name', 99), undefined);
  assert.equal(describe('Relations', 'EntityA', 99), undefined);
});
