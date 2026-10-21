namespace Ara3D.Step.CorpusHarness;

/// <summary>Prints "stage i/n" to standard output every <see cref="Every"/> rows, so the <c>all</c> mode can name the
/// stage and row a file reached when its wall-clock limit kills it.</summary>
public sealed class Progress
{
    public const int Every = 100;

    readonly string _stage;
    readonly int _count;
    int _done;

    public Progress(string stage, int count)
    {
        _stage = stage;
        _count = count;
        Report(_stage, 0, _count);
    }

    public void Step()
    {
        var done = Interlocked.Increment(ref _done);
        if (done % Every == 0 || done == _count)
            Report(_stage, done, _count);
    }

    public static void Report(string stage, int done = 0, int count = 0)
    {
        Console.WriteLine($"stage {stage} {done}/{count}");
        Console.Out.Flush();
    }
}
