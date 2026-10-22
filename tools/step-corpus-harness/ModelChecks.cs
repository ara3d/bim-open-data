using System.Diagnostics;
using System.Reflection;
using Ara3D.StepBrep;

namespace Ara3D.Step.CorpusHarness;

/// <summary>One of layer 7's checks run on one model: the law it states, whether it held, the worst measure against
/// its bound, where the worst case is, and what it cost. <see cref="Verdict"/> is pass, fail, threw, or over budget.</summary>
public sealed record CheckRow(string Stage, string Law, string Verdict, double Worst, double Bound, string Where, double Ms, string Exception)
{
    public const string CsvHeader = "stage,law,verdict,worst,bound,where,ms,exception";

    public bool Fails => Verdict is "fail" or "threw";

    public string Csv()
        => TimeTables.Join(Stage, Law, Verdict, Format.Number(Worst), Format.Number(Bound), Where, Format.Ms(Ms), Exception);
}

/// <summary>The library's laws on a real file's model (TKT-441): every generated <c>Check_*</c> method that needs only
/// the model, or the model and a sample plan (layer 7's <c>CheckPlan</c>), found by reflection so a new check runs
/// without a change here. These are the compiled forms of layer 7's laws, the ones the law runner states over
/// witnesses; here they run on each corpus file's raw decoded model and its prepared model, so a decode or prepare
/// defect is named by the law it breaks. A check that throws is a failing row with the exception. Every check runs
/// on its own thread under a budget: a check still running at the budget is recorded as over budget and left to the
/// process's exit, since the generated code cannot be interrupted (several sampling checks re-test the whole arena
/// at each sample, TKT-335, and take minutes on a corpus file).</summary>
public static class ModelChecks
{
    static readonly MethodInfo[] Checks = typeof(BrepModel).Assembly.GetTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        .Where(m => m.Name.StartsWith("Check_") && m.ReturnType == typeof(Check))
        .Where(m => m.GetParameters() is [{ ParameterType: var a }] && a == typeof(BrepModel)
                    || m.GetParameters() is [{ ParameterType: var b }, { ParameterType: var c }] && b == typeof(BrepModel) && c == typeof(SamplePlan))
        .GroupBy(m => m.Name).Select(g => g.First())
        .OrderBy(m => m.Name, StringComparer.Ordinal)
        .ToArray();

    public static int Count => Checks.Length;

    /// <summary>Seconds one check may take before it is recorded as over budget.</summary>
    public const double BudgetSeconds = 2;

    /// <summary>Checks running at once; an abandoned check gives up its slot, so the budget measures the check, not
    /// a queue.</summary>
    public const int Concurrency = 8;

    public static IReadOnlyList<CheckRow> Run(string stage, BrepModel m)
    {
        var plan = SamplePlan.CheckPlan();
        var budget = TimeSpan.FromSeconds(BudgetSeconds);
        var rows = new CheckRow[Checks.Length];
        Parallel.For(0, Checks.Length, new ParallelOptions { MaxDegreeOfParallelism = Concurrency }, i =>
        {
            var method = Checks[i];
            var clock = Stopwatch.StartNew();
            var task = Task.Factory.StartNew(() => One(stage, m, plan, method, clock), TaskCreationOptions.LongRunning);
            rows[i] = task.Wait(budget)
                ? task.Result
                : new CheckRow(stage, Name(method), "over budget", double.NaN, double.NaN, "", clock.Elapsed.TotalMilliseconds, "");
        });
        return rows;
    }

    static string Name(MethodInfo method) => method.Name["Check_".Length..];

    static CheckRow One(string stage, BrepModel m, SamplePlan plan, MethodInfo method, Stopwatch clock)
    {
        try
        {
            var c = (Check)method.Invoke(null, method.GetParameters().Length == 1 ? [m] : [m, plan])!;
            return new(stage, Name(method), (bool)c.Holds ? "pass" : "fail", c.Worst, c.Bound, Where(c.Where),
                clock.Elapsed.TotalMilliseconds, "");
        }
        catch (TargetInvocationException e)
        {
            var inner = e.InnerException ?? e;
            return new(stage, Name(method), "threw", double.NaN, double.NaN, "", clock.Elapsed.TotalMilliseconds,
                $"{inner.GetType().Name}: {inner.Message}");
        }
    }

    static string Where(Provenance p)
    {
        try { return PlatoLiteral.Write(p); }
        catch (NotSupportedException) { return p.ToString(); }
    }
}
