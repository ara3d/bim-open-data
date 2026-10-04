// node --test bench/ifc-bench/compare.test.mjs
import assert from "node:assert/strict";
import { test } from "node:test";
import { compare, numbers, statesAbsence } from "./compare.mjs";
import { classifyLicence, parseCsv } from "./dataset.mjs";

const q = (category, ground_truth) => ({ category, ground_truth });

test("numbers drop thousands separators and ignore identifiers", () => {
  assert.deepEqual(numbers("1,234.5 m and 12 doors, M_Door:0762 #146596 OG1"), [1234.5, 12]);
});

test("a short numeric ground truth matches within 1 % and across m/mm", () => {
  assert.equal(compare(q(3, "The total length of exterior walls is 464 meters."), "About 465.9 m in total.").verdict, "correct");
  assert.equal(compare(q(1, "The wall is 0.3 m thick."), "Thickness: 300 mm").verdict, "correct");
  assert.equal(compare(q(2, "There are 14 doors."), "The model has 12 doors.").verdict, "wrong");
});

test("a row-count citation is not read as the answer", () => {
  assert.equal(compare(q(2, "There are 3 stairs."), "There are 2 stairs (from ifc_sql, 3 rows).").verdict, "wrong");
});

test("claiming absence when the ground truth has a value is wrong", () => {
  assert.equal(compare(q(1, "The site area is 812 sqm."), "The site area is not available in the model.").verdict, "wrong");
});

test("long or non-numeric ground truths go to review", () => {
  assert.equal(compare(q(2, "No window types found with fire rating information."), "None of the windows has a fire rating.").verdict, "review");
  assert.equal(compare(q(1, "x".repeat(200) + " 5"), "5").verdict, "review");
});

test("category 4 rewards a stated absence and marks an unqualified value wrong", () => {
  const truth = "The information is not directly available in the BIM model; it can be estimated at 275 sqm.";
  assert.equal(compare(q(4, truth), "The model does not contain a facade area quantity.").verdict, "correct");
  assert.equal(compare(q(4, truth), "The facade area is 280 m².").verdict, "wrong");
  assert.ok(statesAbsence("No property for capacity is stored on the spaces."));
});

test("a missing answer is an error", () => {
  assert.equal(compare(q(2, "3"), null).verdict, "error");
});

test("CSV fields may hold quotes, commas and newlines", () => {
  assert.deepEqual(parseCsv('id,text\n1,"a, ""b""\nc"\n2,d\n'), [{ id: "1", text: 'a, "b"\nc' }, { id: "2", text: "d" }]);
});

test("licences are read from their wording", () => {
  assert.equal(classifyLicence("MIT License\n\nCopyright"), "MIT");
  assert.equal(classifyLicence("licensed under the Creative Commons Attribution 4.0 International License"), "CC-BY-4.0");
  assert.equal(classifyLicence("Creative Commons Attribution 3.0 Unported"), "CC-BY-3.0");
  assert.equal(classifyLicence("GNU General Public License"), "GPL-3.0-or-later");
});
