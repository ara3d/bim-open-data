// Grades one run's transcripts and writes results/<run>/: results.json (every question with
// its text, ground truth, answer, verdict, cost, time, and tool calls), results.csv (one line
// each, no free text), transcript.jsonl (tool calls and results, results cut to 600
// characters), and summary.json.
//
//   node grade.mjs --run 2026-10-03
//
// The comparator's verdict is kept in `auto`. When results/<run>/review.json holds a
// reviewer's verdict for a question ({ "<id>": { "verdict": "correct", "note": "..." } }),
// `verdict` takes it and `reviewedBy` names its source; otherwise `verdict` is `auto`.
// A reviewer's `failure` label (or the file's `failures` map, for questions whose comparator
// verdict stands) names why a wrong answer is wrong.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { compare } from "./compare.mjs";
import { cacheDir, here, readJson, readQuestions, repo, revision } from "./dataset.mjs";

const i = process.argv.indexOf("--run");
const runName = i > 0 ? process.argv[i + 1] : new Date().toISOString().slice(0, 10);
const runDir = join(cacheDir, "runs", runName);
const outDir = join(here, "results", runName);
mkdirSync(outDir, { recursive: true });

const run = readJson(join(runDir, "run.json"));
const reviewFile = join(outDir, "review.json");
const review = existsSync(reviewFile) ? readJson(reviewFile) : { reviewer: null, verdicts: {} };
const subset = readJson(join(here, "subset.json")).questions;
const text = new Map(readQuestions().map((q) => [q.id, q]));

function readTranscript(id) {
  const file = join(runDir, `${id}.jsonl`);
  if (!existsSync(file)) return null;
  const messages = readFileSync(file, "utf8").split("\n").filter(Boolean).flatMap((l) => {
    try { return [JSON.parse(l)]; } catch { return []; }
  });
  const result = messages.find((m) => m.type === "result");
  const steps = messages.flatMap((m) => (m.type === "assistant" || m.type === "user")
    ? (m.message?.content ?? []).filter((c) => c.type === "tool_use" || c.type === "tool_result") : []);
  const resultText = (c) => (Array.isArray(c.content) ? c.content.map((p) => p.text ?? "").join("") : String(c.content ?? ""));
  return {
    result,
    runnerError: messages.find((m) => m.type === "runner_error") ?? null,
    calls: steps.filter((c) => c.type === "tool_use").map((c) => ({ tool: c.name.replace("mcp__bimopen-ifc__", ""), input: c.input })),
    steps: steps.map((c) => c.type === "tool_use"
      ? { call: c.name.replace("mcp__bimopen-ifc__", ""), input: c.input }
      : { result: resultText(c).slice(0, 600), isError: c.is_error ?? false }),
  };
}

const rows = subset.map((s) => {
  const q = text.get(s.id);
  const t = readTranscript(s.id);
  const answer = t?.result && !t.result.is_error && t.result.subtype === "success" ? t.result.result : null;
  const auto = !t ? { verdict: "not run", reason: "" } : compare(q, answer);
  if (t && answer === null) auto.reason = t.result?.subtype ?? t.runnerError?.stderr?.slice(-200) ?? "no result";
  const reviewed = review.verdicts[s.id];
  return {
    id: s.id, project: s.project, model: s.ifc_model, category: s.category,
    question: q.question, groundTruth: q.ground_truth, answer,
    auto, verdict: reviewed?.verdict ?? auto.verdict, reviewNote: reviewed?.note ?? null,
    reviewedBy: reviewed ? review.reviewer : null,
    failure: reviewed?.failure ?? review.failures?.[s.id] ?? null,
    costUsd: t?.result?.total_cost_usd ?? null, seconds: t?.result ? Math.round(t.result.duration_ms / 1000) : null,
    turns: t?.result?.num_turns ?? null, toolCalls: t?.calls.length ?? 0, tools: t?.calls ?? [],
    steps: t?.steps ?? [],
  };
}).filter((r) => r.verdict !== "not run");

const tally = (list, pick) => Object.fromEntries(["correct", "wrong", "review", "error"].map((v) => [v, list.filter((r) => pick(r) === v).length]));
const score = (pick) => ({
  total: tally(rows, pick),
  byCategory: Object.fromEntries([1, 2, 3, 4].map((c) => [c, tally(rows.filter((r) => r.category === c), pick)])),
});
const sum = (f) => rows.reduce((n, r) => n + (f(r) ?? 0), 0);
const summary = {
  run: runName, dataset: repo, revision, model: run.model, effort: run.effort, claudeVersion: run.claudeVersion,
  questions: rows.length, reviewer: review.reviewer,
  auto: score((r) => r.auto.verdict),
  final: score((r) => r.verdict),
  costUsd: Math.round(sum((r) => r.costUsd) * 100) / 100,
  sessionSecondsTotal: sum((r) => r.seconds),
  wallSeconds: run.batches.reduce((n, b) => n + b.wallSeconds, 0),
  toolCalls: sum((r) => r.toolCalls),
};

const csvCell = (v) => (/[",\n]/.test(String(v)) ? `"${String(v).replaceAll('"', '""')}"` : String(v ?? ""));
const columns = ["id", "project", "model", "category", "auto", "autoReason", "verdict", "failure", "reviewedBy", "costUsd", "seconds", "turns", "toolCalls"];
const csv = [columns.join(","), ...rows.map((r) => columns.map((c) => csvCell(
  c === "auto" ? r.auto.verdict : c === "autoReason" ? r.auto.reason : r[c])).join(","))].join("\n") + "\n";

writeFileSync(join(outDir, "results.json"), JSON.stringify({
  attribution: "Questions and ground truths: IFC-Bench v2 (Hellin et al. 2026, https://huggingface.co/datasets/sylvainHellin/ifc-bench), CC BY 4.0, unchanged.",
  summary, questions: rows.map(({ steps, ...r }) => r),
}, null, 2) + "\n");
writeFileSync(join(outDir, "transcript.jsonl"), rows.map((r) => JSON.stringify({ id: r.id, steps: r.steps, answer: r.answer })).join("\n") + "\n");
writeFileSync(join(outDir, "results.csv"), csv);
writeFileSync(join(outDir, "summary.json"), JSON.stringify(summary, null, 2) + "\n");
console.log(JSON.stringify(summary, null, 2));
