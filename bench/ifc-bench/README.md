# IFC-Bench evaluation of the IFC MCP server

This folder measures how well an agent answers questions about IFC files when its only tools are this repository's `bimopen-ifc` MCP server (`src/mcp/BimOpenMcp.Ifc`). The questions and expected answers come from [IFC-Bench v2](https://huggingface.co/datasets/sylvainHellin/ifc-bench), an external benchmark of 1,026 questions over 22 building projects in four categories: 1 direct retrieval, 2 aggregation, 3 geometric or spatial computation, 4 information the model does not hold. Toolkit ticket TKT-145 asked for it; results are in [RESULTS.md](RESULTS.md).

The agent is Claude Haiku 4.5 (`claude-haiku-4-5-20251001`, medium effort) run through the Claude Code command line, `claude -p`, one fresh session per question. It sees the bimopen-ifc tools except the two that write files, and `.claude/skills/ifc-ask/ifc-guide.md` as its appended system prompt. No other tools, settings, skills, or CLAUDE.md files are loaded.

## Files

| File | What it holds |
|---|---|
| `dataset.mjs` | The pinned dataset revision, the cache location, the CSV reader, and the licence rule |
| `fetch.mjs` | Downloads questions, licences, and model cards; writes `manifest.json`. `--models` downloads the IFC files the subset needs |
| `manifest.json` | Every project with its licence, whether this evaluation may use it, and each model's size and question counts by category |
| `select.mjs`, `subset.json` | The 100-question stratified subset, by question id |
| `run.mjs` | Runs `claude -p` on each subset question; transcripts go to `cache/runs/<run>/` |
| `compare.mjs`, `compare.test.mjs` | The deterministic comparator and its tests |
| `grade.mjs` | Grades a run into `results/<run>/` |
| `results/<run>/` | `results.json` (question, ground truth, answer, verdict, cost), `results.csv`, `transcript.jsonl` (tool calls and shortened results), `summary.json`, and `review.json` when a reviewer graded the questions the comparator could not |

`cache/` is git-ignored: it holds the downloaded dataset (about 0.4 GB for the subset's 22 models) and the full transcripts.

## Running it

```powershell
dotnet build src/mcp/BimOpenMcp.Ifc/BimOpenMcp.Ifc.csproj -c Release -o artifacts/ifc-bench-mcp
node bench/ifc-bench/fetch.mjs            # metadata and manifest.json
node bench/ifc-bench/select.mjs           # subset.json (already committed; reruns give the same ids)
node bench/ifc-bench/fetch.mjs --models   # the subset's IFC files
node bench/ifc-bench/run.mjs --run 2026-10-03 --limit 5     # a pilot
node bench/ifc-bench/run.mjs --run 2026-10-03               # the rest; finished questions are skipped
node bench/ifc-bench/grade.mjs --run 2026-10-03
node --test bench/ifc-bench/compare.test.mjs
```

`claude` must be on the PATH and signed in. Running all 1,026 questions is not wired up on purpose: the owner approves that cost first (see RESULTS.md for the estimate).

## Which projects are used

The subset uses only projects whose models are CC BY 4.0 or MIT, read from each project's `license.txt` (`manifest.json` records the result), and only model files of at most 80 MB, so no question waits minutes on a conversion. Excluded by licence: `4351`, `ettenheim_gis`, `hitos`, and `samuel_macalister_sample_house` (GPL 3.0), and `west_riverside_hospital` (CC BY 3.0, permissive but outside the ticket's rule). Excluded by size: `sixty5` architecture, electrical, plumbing, and ventilation, and `dental_clinic` MEP (which has no questions). `city_house_munich` and `fantasy_hotel_2` have no questions in v2.

`wbdg_office/license.txt` names the buildingSMART Medical-Dental test files as its source, the same text as `dental_clinic`; it is probably a copy error in the dataset, and the licence it states is CC BY 4.0 either way.

## Attribution

IFC-Bench: Sylvain Hellin, Technical University of Munich, CC BY 4.0. Cite Hellin, Jang, Fuchs, Nousias, and Borrmann, "Agentic Search for BIM Information Extraction: Systematic Evaluation", *Automation in Construction* 192 (2026) 107260, [doi:10.1016/j.autcon.2026.107260](https://doi.org/10.1016/j.autcon.2026.107260). `results/*/results.json` reproduces the subset's questions and ground truths unchanged.

The IFC models are not committed. Their owners, all under CC BY 4.0 unless marked MIT: KIT IAI (`ac20`, `fzk_house`, `smiley_west`); buildingSMART International (`dental_clinic`, `duplex`, `molio`, `sixty5`, `wbdg_office`); RWTH Aachen E3D (`digital_hub`, MIT); TUM BIM Fundamentals SS2025 students (`fantasy_*`, MIT); the original owners of `schependomlaan` via the openBIM Archive.
