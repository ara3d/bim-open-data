// Answers subset questions with the bimopen-ifc MCP server driven by the Claude Code command
// line (`claude -p`), one fresh session per question, and keeps each session's stream-json
// transcript in cache/runs/<run>/<id>.jsonl. A question whose transcript already ends in a
// result is skipped, so a stopped run resumes where it stopped.
//
//   node run.mjs --run 2026-10-03 [--limit 5 | --ids 12,40] [--concurrency 3]
//                [--model claude-haiku-4-5-20251001] [--effort medium] [--server <dll>]
//
// The server dll defaults to ../../artifacts/ifc-bench-mcp/bimopenmcp-ifc.dll, built with
//   dotnet build src/mcp/BimOpenMcp.Ifc/BimOpenMcp.Ifc.csproj -c Release -o artifacts/ifc-bench-mcp
// The agent gets only the bimopen-ifc tools, minus the two that write files, and the
// repository's ifc-ask guide as its appended system prompt.
import { spawn, execSync } from "node:child_process";
import { createWriteStream, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { cacheDir, here, modelPath, readJson, readQuestions } from "./dataset.mjs";

const arg = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i > 0 ? process.argv[i + 1] : fallback;
};
const runName = arg("--run", new Date().toISOString().slice(0, 10));
const model = arg("--model", "claude-haiku-4-5-20251001");
const effort = arg("--effort", "medium");
const concurrency = Number(arg("--concurrency", "3"));
const maxTurns = arg("--turns", "30");
const timeoutMs = Number(arg("--timeout-min", "15")) * 60_000;
const server = resolve(arg("--server", join(here, "../../artifacts/ifc-bench-mcp/bimopenmcp-ifc.dll")));
const guide = resolve(here, "../../.claude/skills/ifc-ask/ifc-guide.md");

const runDir = join(cacheDir, "runs", runName);
mkdirSync(runDir, { recursive: true });
if (!existsSync(server)) throw new Error(`MCP server not built: ${server}`);

const subset = readJson(join(here, "subset.json")).questions;
const text = new Map(readQuestions().map((q) => [q.id, q]));
const ids = arg("--ids", null)?.split(",").map(Number);
const limit = Number(arg("--limit", "0"));
let chosen = ids ? subset.filter((q) => ids.includes(q.id)) : subset;
if (limit > 0) chosen = chosen.slice(0, limit);

const mcpConfig = join(runDir, "mcp.json");
writeFileSync(mcpConfig, JSON.stringify({ mcpServers: { "bimopen-ifc": { command: "dotnet", args: [server] } } }));

const prompt = (q, path) =>
  `IFC model: ${path.replaceAll("\\", "/")}\n` +
  `Question: ${q.question}\n\n` +
  "Answer the question about this model using the bimopen-ifc tools. " +
  "End with the answer itself in a few lines.";

/** On Windows `claude` is usually a .cmd shim, which Node starts only through cmd.exe; the
 * prompt goes on stdin and every argument is quoted, so no argument holds a newline. */
function startClaude(args, cwd) {
  const options = { cwd, stdio: ["pipe", "pipe", "pipe"] };
  if (process.platform !== "win32") return spawn("claude", args, options);
  return spawn(["claude", ...args.map((a) => `"${a}"`)].join(" "), { ...options, shell: true });
}

const finished = (file) => existsSync(file)
  && readFileSync(file, "utf8").split("\n").some((l) => {
    try { return JSON.parse(l).type === "result"; } catch { return false; }
  });

function ask(q) {
  const file = join(runDir, `${q.id}.jsonl`);
  if (finished(file)) return Promise.resolve("cached");
  const args = [
    "-p",
    "--model", model, "--effort", effort, "--max-turns", maxTurns,
    "--output-format", "stream-json", "--verbose",
    "--mcp-config", mcpConfig, "--strict-mcp-config",
    "--tools", "", "--allowedTools", "mcp__bimopen-ifc",
    "--disallowedTools", "mcp__bimopen-ifc__ifc_export_glb,mcp__bimopen-ifc__ifc_sql_export",
    "--setting-sources", "", "--disable-slash-commands", "--no-session-persistence",
    "--permission-mode", "dontAsk", "--append-system-prompt-file", guide,
  ];
  return new Promise((done) => {
    const out = createWriteStream(file);
    const started = Date.now();
    const child = startClaude(args, runDir);
    child.on("error", (e) => { stderr += String(e); });
    child.stdin.end(prompt(text.get(q.id), modelPath(q.project, q.ifc_model)));
    const timer = setTimeout(() => child.kill(), timeoutMs);
    child.stdout.pipe(out);
    let stderr = "";
    child.stderr.on("data", (d) => { stderr += d; });
    child.on("close", (code) => {
      clearTimeout(timer);
      out.end(() => {
        if (!finished(file))
          writeFileSync(file, readFileSync(file, "utf8") + JSON.stringify({
            type: "runner_error", exitCode: code, elapsedMs: Date.now() - started, stderr: stderr.slice(-2000),
          }) + "\n");
        done(code === 0 ? "ok" : `exit ${code}`);
      });
    });
  });
}

const started = new Date();
let next = 0, completed = 0;
async function worker() {
  while (next < chosen.length) {
    const q = chosen[next++];
    const status = await ask(q);
    console.log(`[${++completed}/${chosen.length}] ${q.id} ${q.project}/${q.ifc_model} cat ${q.category}: ${status}`);
  }
}
await Promise.all(Array.from({ length: concurrency }, worker));
const ended = new Date();

const runFile = join(runDir, "run.json");
const previous = existsSync(runFile) ? readJson(runFile) : { batches: [] };
writeFileSync(runFile, JSON.stringify({
  model, effort, maxTurns: Number(maxTurns), concurrency,
  claudeVersion: execSync("claude --version", { encoding: "utf8" }).trim(),
  batches: [...previous.batches, {
    started: started.toISOString(), ended: ended.toISOString(),
    wallSeconds: Math.round((ended - started) / 1000), questions: chosen.map((q) => q.id),
  }],
}, null, 2) + "\n");
console.log(`wall time ${Math.round((ended - started) / 1000)} s`);
