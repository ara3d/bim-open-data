namespace Ara3D.Step.CorpusHarness;

public static class Rows
{
    /// <summary>One row per index, computed by <paramref name="row"/>, in index order or on every core. Each row
    /// catches its own exceptions (StepPlatoMesher.Rows' isolation), so a parallel run fails the same rows as a
    /// sequential one.</summary>
    public static T[] Build<T>(int count, bool parallel, Func<int, T> row, Progress progress)
    {
        var rows = new T[count];
        if (parallel)
            Parallel.For(0, count, i => { rows[i] = row(i); progress.Step(); });
        else
            for (var i = 0; i < count; i++)
            {
                rows[i] = row(i);
                progress.Step();
            }
        return rows;
    }
}
