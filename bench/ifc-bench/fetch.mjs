// Downloads IFC-Bench at the pinned revision into the git-ignored cache/ folder.
//
//   node fetch.mjs            questions, licences, model cards; checks the questions' SHA-256;
//                             writes manifest.json (projects, licences, model sizes, question counts)
//   node fetch.mjs --models   also the IFC files subset.json needs (about 0.3 GB); run select.mjs first
import { createHash } from "node:crypto";
import { createWriteStream, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { Readable } from "node:stream";
import { pipeline } from "node:stream/promises";
import {
  classifyLicence, datasetDir, fileUrl, here, questionsPath, questionsSha256, readJson,
  readQuestions, repo, revision, treeUrl, usableLicences,
} from "./dataset.mjs";

async function download(path, bytes) {
  const target = join(datasetDir, path);
  if (existsSync(target) && (bytes === undefined || statSync(target).size === bytes)) return target;
  mkdirSync(dirname(target), { recursive: true });
  const response = await fetch(fileUrl(path));
  if (!response.ok) throw new Error(`${path}: HTTP ${response.status}`);
  await pipeline(Readable.fromWeb(response.body), createWriteStream(target));
  console.log(`downloaded ${path}`);
  return target;
}

async function listFiles() {
  const response = await fetch(treeUrl);
  if (!response.ok) throw new Error(`dataset listing: HTTP ${response.status}`);
  return (await response.json()).siblings.map((s) => ({ path: s.rfilename, bytes: s.size }));
}

async function fetchMetadata() {
  const files = await listFiles();
  const small = files.filter((f) => /(^|\/)(license\.txt|model_card\.md|LICENSE|README\.md)$/.test(f.path)
    || f.path === questionsPath);
  for (const f of small) await download(f.path, f.bytes);

  const sha = createHash("sha256").update(readFileSync(join(datasetDir, questionsPath))).digest("hex");
  if (sha !== questionsSha256) throw new Error(`${questionsPath} SHA-256 is ${sha}, expected ${questionsSha256}`);

  const questions = readQuestions();
  const projects = [...new Set(files.map((f) => f.path.match(/^projects\/([^/]+)\//)?.[1]).filter(Boolean))].sort();
  const manifest = {
    dataset: repo, revision, questionsSha256, questions: questions.length,
    rule: `Models are usable here when their licence is one of ${[...usableLicences].join(", ")} (TKT-145).`,
    projects: projects.map((project) => {
      const licenceText = readFileSync(join(datasetDir, "projects", project, "license.txt"), "utf8");
      const licence = classifyLicence(licenceText);
      return {
        project, licence, usable: usableLicences.has(licence),
        licenceHolder: licenceText.split(/\r?\n/).find((l) => l.trim()).trim(),
        models: files.filter((f) => f.path.startsWith(`projects/${project}/`) && f.path.endsWith(".ifc"))
          .map((f) => {
            const model = f.path.split("/").pop().replace(/\.ifc$/, "");
            const own = questions.filter((q) => q.project === project && q.ifc_model === model);
            return { model, bytes: f.bytes, questionsByCategory: [1, 2, 3, 4].map((c) => own.filter((q) => q.category === c).length) };
          }),
      };
    }),
  };
  writeFileSync(join(here, "manifest.json"), JSON.stringify(manifest, null, 2) + "\n");
  console.log(`manifest.json: ${projects.length} projects, ${questions.length} questions`);
}

async function fetchModels() {
  const subset = readJson(join(here, "subset.json"));
  const sizes = new Map((await listFiles()).map((f) => [f.path, f.bytes]));
  const needed = [...new Set(subset.questions.map((q) => `projects/${q.project}/${q.ifc_model}.ifc`))];
  for (const path of needed) await download(path, sizes.get(path));
  console.log(`${needed.length} models present`);
}

await (process.argv.includes("--models") ? fetchModels() : fetchMetadata());
