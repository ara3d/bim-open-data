# IFC-Bench results

## Run of 2026-10-03

100 questions from IFC-Bench v2 (dataset revision `df630de`), answered by Claude Haiku 4.5 (`claude-haiku-4-5-20251001`, medium effort, at most 30 turns) through `claude -p` 2.1.286 with the `bimopen-ifc` MCP server as its only tools. How the 100 were picked, and which projects were left out and why, is in [README.md](README.md). Per-question data: [results/2026-10-03/](results/2026-10-03/).

| Category | Questions | Correct | Wrong | Unresolved | Turn limit |
|---|---:|---:|---:|---:|---:|
| 1 Direct retrieval | 15 | 8 | 5 | 1 | 1 |
| 2 Aggregation | 55 | 39 | 14 | 2 | 0 |
| 3 Geometric or spatial | 11 | 3 | 8 | 0 | 0 |
| 4 Information not available | 19 | 12 | 2 | 4 | 1 |
| **All** | **100** | **62** | **29** | **7** | **2** |

**How the verdicts were reached.** The deterministic comparator (`compare.mjs`) settled 38 questions: 20 correct, 16 wrong, 2 errors. The other 62 have free-text ground truths (lists of types, rooms, layers) that a rule cannot score, so the comparator marked them for review. The agent that ran the evaluation (Claude Opus 5.5) then reviewed every question against its ground truth and recorded a verdict and a reason in `review.json`; the table above uses those verdicts. The owner has not checked them. The review changed 10 of the comparator's 38 verdicts:

- Question 213 went from correct to wrong. The comparator matched the ground truth's 301,106 (mm) to the answer's basement figure of 300.58 m through its m/mm scaling, while the answer's total, 602.21 m, is twice the truth.
- Five category 4 questions went from wrong to correct (85, 180, 396, 740, 1008). The rule "a category 4 answer must say the information is not available" is too strict where the honest answer is "there are none" (no beams, no dampers) or where the ground truth itself gives counts.
- Four category 4 questions went from wrong to unresolved (210, 343, 502, 946). Each answer cites a value from the file, such as 43,822 W of heating power from the `Energieanalyse` property set, where the ground truth says the information is absent. Either the ground truth is wrong or the agent misread the file; someone has to open the file to decide.

**Cost and time.** The CLI reports $6.79 at list price for the 100 sessions: $0.068 a question on average, $0.053 median, $0.21 at most. The sessions ran on a subscription login (no API key), so this is usage against the plan, not a bill. Wall time was 2,738 s (46 min) with three sessions at a time; the sessions themselves took 7,270 s, 73 s each on average and 450 s at most. The agent made 1,036 tool calls, about 10 per question. Converting a model to DuckDB is repeated in every session, because each `claude -p` starts its own server.

## Why answers were wrong

| Cause | Questions |
|---|---|
| Answered about the wrong elements, storey, or grouping | 51, 74, 122, 497, 925, 1004 |
| Incomplete: a list or sum that stops short | 135, 610, 928, 1001 |
| Material layers and their thicknesses not found | 387, 421, 434 (and 457 ran out of turns on the same question type) |
| A join that counts every element twice | 213, 236 |
| Arithmetic done in the answer text instead of in SQL | 375, 440 |
| Geometric or spatial reasoning | 250, 342, 490 |
| Other misreadings: a count off by one, a swapped latitude and longitude, a missing version, a wrong measure, a wrong total, a level assignment | 55, 202, 230, 291, 328, 471, 880 |
| Category 4: a definite answer with no word that the information is absent (253 also says the Duplex has one wall) | 103, 253 |
| Turn limit (30) reached without an answer | 457, 460 |

Category 3 is the weakest at 3 of 11. Its questions ask for footprints, facade areas, and spans, which need geometry the tools give only as bounding boxes and volumes.

## Examples

- **Correct, 302** (Duplex MEP): "How many light fixtures are specified, and what is the breakdown by type?" The answer gives 14 fixtures, 8 pendant and 6 sconce, as the ground truth does, and notes that they are modelled as `IFCFLOWTERMINAL`.
- **Honest absence, 126** (dental clinic structure): asked for the roofing material's expected lifespan, the answer says it is not available, names the two roofing materials, and says the `ExpectedLife` fields hold label text rather than values.
- **Double count, 213** (DigitalHub plumbing): the SQL joined `ParameterText` (`Abmessungen.Länge`) to `StoreyOfElement` and returned 602.21 m over 504 pipes; the ground truth is 301.1 m. In 236 the same join returned 96 area rows for 48 ducts, and the answer said so without correcting for it. The cause is not yet isolated: either the converted model holds two rows for each of these parameters, or `StoreyOfElement` holds two rows for each element.
- **Arithmetic, 440** (fantasy office 3): the answer lists all 17 spaces with the right areas, which sum to 299.30 m², then states a total of 355.30 m².
- **Material layers, 421** (fantasy office 2): every wall type's material comes back as "Generisch"; the ground truth names Ortbeton and Kalksandstein layers.
- **Invented estimate, 103** (dental clinic): asked for the ratio of clinical to non-clinical space, which the model does not record, the answer classifies the rooms itself and reports 1:8.1 without saying that the classification is its own.

## Leads for the toolkit

1. The double count in 213 and 236: find whether `ParameterText` or `StoreyOfElement` duplicates rows for DigitalHub's Revit property sets. It may be the same defect as toolkit TKT-18 (the IFC ask double count).
2. Material layer sets: check whether `IfcMaterialLayerSet` layers and thicknesses reach the converted tables and the tools. Four questions failed on them.
3. The ifc-ask guide could say "compute every total in SQL, never in the answer" (375, 440) and "check that a join returns one row per element before summing" (213, 236).
4. Category 3 needs geometric measures (footprint, facade area) that the tools do not compute today.

## What the full set would cost

At this run's average of $0.068 and 73 s a question, with three sessions at a time:

| Scope | Questions | List-price cost | Wall time |
|---|---:|---:|---:|
| Every eligible question (CC BY 4.0 or MIT, models up to 80 MB) | 733 | about $50 | about 5 h |
| Adding the five large Sixty5 and dental clinic models (97 to 343 MB) | 837 | about $57, likely more | 6 h or more, since each session converts the large file again |
| Adding the GPL 3.0 projects (`4351`, `ettenheim_gis`, `hitos`, `samuel_macalister_sample_house`) | 1,026 | about $70 | about 7 h |

`west_riverside_hospital` (CC BY 3.0) has no questions in v2.
