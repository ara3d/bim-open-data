using Ara3D.IO.StepParser;
using Ara3D.Step.Decode;
using Ara3D.Step.Entities;
using Ara3D.StepBrep;
using Ara3D.Utils;

namespace Ara3D.Step.CorpusHarness;

/// <summary><c>laws file prefix</c>: layer 7's checks (<see cref="ModelChecks"/>) on the file's raw decoded model (under the
/// prepared model's tolerances) and on its prepared model, written to prefix.laws.csv, one row per check and stage; the last line printed, which the all
/// mode keeps in all.csv with <c>--child laws</c>, counts the failing checks of each stage and the time they took. No
/// meshing: a run over the corpus costs parse, decode, prepare and the checks, each check under
/// <see cref="ModelChecks.BudgetSeconds"/>.</summary>
public static class LawsMode
{
    public static int Run(string file, string prefix, Options options)
    {
        try
        {
            Progress.Report("parse");
            var (model, parseMs) = Clock.Time(() => new StepModel(new StepDocument(new FilePath(file))));
            Progress.Report("decode");
            var (raw, decodeMs) = Clock.Time(() => model.DecodeRaw());
            Progress.Report("prepare");
            var (prepared, prepareMs) = Clock.Time(() => raw.PrepareModel());
            // The raw model's tolerances are zero until PrepareModel decides them, so every check with a tolerance
            // would fail on it by construction: the raw stage runs under the prepared model's tolerances, and a
            // check failing there and not on the prepared model names a defect PrepareModel repairs.
            Progress.Report("laws raw");
            var (rawRows, rawMs) = Clock.Time(() => ModelChecks.Run("raw", raw.Model.WithTolerances(prepared.Model.Tolerances)));
            Progress.Report("laws prepared");
            var (preparedRows, preparedMs) = Clock.Time(() => ModelChecks.Run("prepared", prepared.Model));
            var rows = rawRows.Concat(preparedRows).ToArray();
            TimeTables.Write(prefix + ".laws.csv", CheckRow.CsvHeader, rows.Select(r => r.Csv()).ToArray());
            string Tally(IReadOnlyList<CheckRow> rs)
                => string.Join(", ", rs.GroupBy(r => r.Verdict).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
            string Failing(IReadOnlyList<CheckRow> rs) => string.Join(" ", rs.Where(r => r.Fails).Select(r => r.Law));
            Console.WriteLine($"laws {ModelChecks.Count} checks; raw {Tally(rawRows)} in {Format.Ms(rawMs)} ms; " +
                              $"prepared {Tally(preparedRows)} in {Format.Ms(preparedMs)} ms; " +
                              $"parse {Format.Ms(parseMs)} decode {Format.Ms(decodeMs)} prepare {Format.Ms(prepareMs)} ms; " +
                              $"raw: {Failing(rawRows)}; prepared: {Failing(preparedRows)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine($"laws failed: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
