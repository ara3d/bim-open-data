// Picks the evaluation subset and writes subset.json: question ids with their project, model,
// and category. The questions' text stays in the cache; results carry it with attribution.
//
//   node select.mjs [--size 100] [--max-model-mb 80]
//
// Eligible: the model's project licence is usable (dataset.mjs) and the file is at most
// --max-model-mb, so one question does not wait minutes on a conversion. Each category gets
// seats in proportion to its share of the whole dataset (largest remainder). Inside a category,
// models take turns, and each model's questions are ordered by SHA-256 of their id, so the
// pick is spread over buildings and the same on every machine.
import { createHash } from "node:crypto";
import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { here, readJson, readQuestions, revision } from "./dataset.mjs";

const arg = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i > 0 ? Number(process.argv[i + 1]) : fallback;
};
const size = arg("--size", 100);
const maxModelMb = arg("--max-model-mb", 80);

const manifest = readJson(join(here, "manifest.json"));
const eligibleModels = new Set(manifest.projects.filter((p) => p.usable).flatMap((p) =>
  p.models.filter((m) => m.bytes <= maxModelMb * 1e6).map((m) => `${p.project}/${m.model}`)));

const all = readQuestions();
const pool = all.filter((q) => eligibleModels.has(`${q.project}/${q.ifc_model}`));
const rank = (q) => createHash("sha256").update(`ifc-bench-subset:${q.id}`).digest("hex");

/** Seats per category, proportional to the whole dataset, by largest remainder. */
function quotas() {
  const shares = [1, 2, 3, 4].map((c) => ({ c, exact: size * all.filter((q) => q.category === c).length / all.length }));
  shares.forEach((s) => { s.seats = Math.floor(s.exact); });
  const left = size - shares.reduce((n, s) => n + s.seats, 0);
  [...shares].sort((a, b) => (b.exact % 1) - (a.exact % 1)).slice(0, left).forEach((s) => { s.seats++; });
  return shares;
}

function pick(category, seats) {
  const byModel = new Map();
  for (const q of pool.filter((q) => q.category === category).sort((a, b) => rank(a).localeCompare(rank(b)))) {
    const key = `${q.project}/${q.ifc_model}`;
    byModel.set(key, [...(byModel.get(key) ?? []), q]);
  }
  const queues = [...byModel.keys()].sort().map((k) => byModel.get(k));
  const chosen = [];
  while (chosen.length < seats && queues.some((q) => q.length > 0))
    for (const queue of queues) if (queue.length > 0 && chosen.length < seats) chosen.push(queue.shift());
  return chosen;
}

const questions = quotas().flatMap(({ c, seats }) => pick(c, seats))
  .map(({ id, project, ifc_model, category }) => ({ id, project, ifc_model, category }))
  .sort((a, b) => a.id - b.id);

writeFileSync(join(here, "subset.json"), JSON.stringify({
  revision, size: questions.length, maxModelMb,
  rule: "usable licence, model at most maxModelMb, category seats proportional to the dataset, models take turns, order by sha256('ifc-bench-subset:'+id)",
  questions,
}, null, 2) + "\n");
const count = (c) => questions.filter((q) => q.category === c).length;
console.log(`subset.json: ${questions.length} questions from ${pool.length} eligible; by category ${[1, 2, 3, 4].map(count).join("/")}; `
  + `${new Set(questions.map((q) => q.project + "/" + q.ifc_model)).size} models`);
