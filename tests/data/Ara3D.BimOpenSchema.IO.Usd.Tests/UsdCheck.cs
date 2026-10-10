using System.Diagnostics;
using System.Text.Json;

namespace Ara3D.BimOpenSchema.IO.Usd.Tests;

/// <summary>
/// Runs check_usda.py under a Python that has usd-core (the pxr package), so a stage is
/// opened and validated by real USD. The Python is the one named by the BOS_USD_PYTHON
/// environment variable, else "python" on PATH. When neither imports pxr the calling test
/// is ignored with the reason, so a machine without usd-core still passes but says so.
/// </summary>
internal static class UsdCheck
{
    public const string PythonVariable = "BOS_USD_PYTHON";

    private static readonly Lazy<(string? Python, string Reason)> Found = new(FindPython);

    /// <summary>The check's JSON output for the stage, with the bim: attributes of each prim in
    /// <paramref name="primPaths"/>. Ignores the test when no Python with pxr is found.</summary>
    public static JsonElement Run(string stagePath, params string[] primPaths)
    {
        var (python, reason) = Found.Value;
        if (python is null)
            Assert.Ignore(reason);
        var script = Path.Combine(AppContext.BaseDirectory, "check_usda.py");
        var (exit, stdout, stderr) = Execute(python!, [script, stagePath, .. primPaths], TimeSpan.FromMinutes(10));
        Assert.That(exit, Is.EqualTo(0), $"check_usda.py failed:\n{stderr}");
        TestContext.Out.WriteLine($"usd-core check ran with {python}");
        if (stderr.Length > 0)
            TestContext.Out.WriteLine($"usd-core stderr:\n{stderr}");
        return JsonDocument.Parse(stdout).RootElement;
    }

    private static (string? Python, string Reason) FindPython()
    {
        var configured = Environment.GetEnvironmentVariable(PythonVariable);
        var candidate = string.IsNullOrWhiteSpace(configured) ? "python" : configured;
        try
        {
            var (exit, _, stderr) = Execute(candidate, ["-c", "import pxr"], TimeSpan.FromMinutes(1));
            return exit == 0
                ? (candidate, "")
                : (null, $"usd-core check skipped: '{candidate}' cannot import pxr ({stderr.Trim()}). " +
                         $"Install it with 'python -m pip install usd-core', or set {PythonVariable} to a Python that has it.");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return (null, $"usd-core check skipped: '{candidate}' did not start ({e.Message}). " +
                          $"Set {PythonVariable} to a Python with usd-core installed.");
        }
    }

    private static (int Exit, string Stdout, string Stderr) Execute(string file, IEnumerable<string> args, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
            info.ArgumentList.Add(a);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"{file} timed out after {timeout}");
        }
        return (process.ExitCode, stdout.Result, stderr.Result);
    }
}
