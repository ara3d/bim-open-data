// Where IFC-Bench comes from, where it is cached, and how its CSV and licences are read.
// Every other script in this folder imports from here, so the pinned revision and the
// licence rule each live in one place.
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

export const here = dirname(fileURLToPath(import.meta.url));
export const cacheDir = join(here, "cache");
export const datasetDir = join(cacheDir, "dataset");

export const repo = "sylvainHellin/ifc-bench";
/** The dataset commit every number in this folder was measured against (tag v2.0.2's head on 2026-10-03). */
export const revision = "df630deb761efcb184f1a30ab374da7dbae38225";
/** SHA-256 of questions/ifc-bench-v2.csv that the dataset README publishes. */
export const questionsSha256 = "1a3ab5f824e3805b435a72ff672adb89228609e8446a7efdef9bc771f2df570a";
export const questionsPath = "questions/ifc-bench-v2.csv";

export const fileUrl = (path) =>
  `https://huggingface.co/datasets/${repo}/resolve/${revision}/${path.split("/").map(encodeURIComponent).join("/")}`;
export const treeUrl = `https://huggingface.co/api/datasets/${repo}/revision/${revision}?blobs=true`;

/** Licences under which this evaluation may use a project's models (the TKT-145 rule). */
export const usableLicences = new Set(["CC-BY-4.0", "MIT"]);

/** Classifies a project's license.txt by its wording. Unknown text stays "unknown", never guessed. */
export function classifyLicence(text) {
  if (/Attribution 4\.0/i.test(text)) return "CC-BY-4.0";
  if (/Attribution 3\.0/i.test(text)) return "CC-BY-3.0";
  if (/^\s*MIT License/i.test(text)) return "MIT";
  if (/GNU General Public License/i.test(text)) return "GPL-3.0-or-later";
  return "unknown";
}

/** RFC 4180 CSV: quoted fields may hold commas, doubled quotes, and newlines. */
export function parseCsv(text) {
  const rows = [];
  let row = [], field = "", quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') { field += '"'; i++; }
      else if (c === '"') quoted = false;
      else field += c;
    } else if (c === '"') quoted = true;
    else if (c === ",") { row.push(field); field = ""; }
    else if (c === "\n" || c === "\r") {
      if (c === "\r" && text[i + 1] === "\n") i++;
      row.push(field); rows.push(row); row = []; field = "";
    } else field += c;
  }
  if (field !== "" || row.length > 0) { row.push(field); rows.push(row); }
  const [header, ...body] = rows.filter((r) => r.length > 1 || r[0] !== "");
  return body.map((r) => Object.fromEntries(header.map((h, i) => [h, r[i] ?? ""])));
}

/** The v2 questions from the cache, with id and category as numbers. Run fetch.mjs first. */
export function readQuestions() {
  const text = readFileSync(join(datasetDir, questionsPath), "utf8").replace(/^﻿/, "");
  return parseCsv(text).map((q) => ({ ...q, id: Number(q.id), category: Number(q.category) }));
}

export const readJson = (path) => JSON.parse(readFileSync(path, "utf8"));
export const modelPath = (project, model) => join(datasetDir, "projects", project, `${model}.ifc`);
