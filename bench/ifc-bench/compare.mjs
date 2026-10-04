// The deterministic comparator. It returns a verdict only when a rule settles it, and
// "review" otherwise: a free-text ground truth that lists types, layers, or rooms is left
// to a person rather than scored by a guess.
//
// Verdicts: correct, wrong, review, error.
//   Category 4 (information not available): an answer that says the information is absent
//   is correct; one that gives a value without saying so is wrong (TKT-145).
//   Categories 1-3: a short ground truth with one to three numbers is correct when every
//   number appears in the answer (1 % relative tolerance, or the same value in m/mm/cm),
//   wrong when the answer gives numbers and none of them match, or claims the information
//   is absent; anything else goes to review.

const absence = new RegExp([
  String.raw`not (directly |explicitly )?(available|stored|present|included|contained|recorded|modell?ed|specified|defined|provided|populated|assigned|given|found in the model)`,
  String.raw`(does not|doesn't|do not|don't) (contain|carry|include|have|store|record|specify|define|provide|hold)`,
  String.raw`no (data|information|property|properties|values?|attribute|quantit(y|ies)) (for|on|about|is|are|was|were|that|to|in)`,
  String.raw`(cannot|can't|could not|couldn't|unable to) (be )?(determine|determined|calculate|calculated|answer|answered|derive|derived|find|found|establish|established|identify|identified)`,
  String.raw`(is|are) (absent|missing|null|empty)`,
  String.raw`information (is )?not available`,
  String.raw`not available`,
].join("|"), "i");

export const statesAbsence = (answer) => absence.test(answer);

/** Numbers in a text: thousands separators removed, identifiers like "M_Door:0762" ignored. */
export function numbers(text) {
  const found = [];
  const pattern = /(?<![\w.:#-])-?\d{1,3}(?:,\d{3})+(?:\.\d+)?(?![\w])|(?<![\w.:#-])-?\d+(?:\.\d+)?/g;
  for (const m of text.matchAll(pattern)) found.push(Number(m[0].replaceAll(",", "")));
  return found.filter((n) => Number.isFinite(n));
}

const close = (a, b) => Math.abs(a - b) <= Math.max(Math.abs(b) * 0.01, 0.005);
/** Same value, allowing the answer to use m, cm, or mm where the ground truth used another. */
export const matches = (answerNumber, truth) =>
  [1, 1000, 0.001, 100, 0.01].some((scale) => close(answerNumber * scale, truth));

/** Drops the "from ifc_sql, 3 rows" citations the ifc-ask guide asks for, so a row count is
 * not read as the answer. */
export const withoutCitations = (answer) =>
  answer.replace(/\b\d[\d,]*\s+(rows?|results?|records?)\b/gi, " ").replace(/\bifc_\w+/g, " ");

const shortTruth =(truth) => truth.length <= 160;

export function compare({ category, ground_truth: truth }, answer) {
  if (answer === null || answer === undefined) return { verdict: "error", reason: "no answer" };
  if (category === 4)
    return statesAbsence(answer)
      ? { verdict: "correct", reason: "says the information is not available" }
      : { verdict: "wrong", reason: "gives an answer without saying the information is not available" };

  const expected = numbers(truth);
  if (!shortTruth(truth) || expected.length === 0 || expected.length > 3)
    return { verdict: "review", reason: expected.length === 0 ? "ground truth has no number" : "ground truth is long or lists many numbers" };

  const given = numbers(withoutCitations(answer));
  const found = expected.filter((e) => given.some((g) => matches(g, e)));
  if (found.length === expected.length) {
    const small = expected.every((e) => Number.isInteger(e) && Math.abs(e) < 10);
    return small && given.length > 6
      ? { verdict: "review", reason: "expected small number appears among many numbers" }
      : { verdict: "correct", reason: `found ${expected.join(", ")}` };
  }
  if (statesAbsence(answer) && found.length === 0)
    return { verdict: "wrong", reason: "says not available; ground truth has a value" };
  if (found.length === 0 && given.length > 0)
    return { verdict: "wrong", reason: `expected ${expected.join(", ")}; none in the answer` };
  return { verdict: "review", reason: `found ${found.length} of ${expected.length} expected numbers` };
}
