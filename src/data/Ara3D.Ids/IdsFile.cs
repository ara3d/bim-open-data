using Microsoft.Extensions.Logging;
using Xbim.InformationSpecifications;

namespace Ara3D.Ids;

/// <summary>An IDS as loaded: the parsed document and what the loader complained about. The
/// loader skips an element it does not know (a misspelled facet, a misplaced value) and logs
/// it instead of failing, so the warnings are the only sign that a specification lost a
/// requirement; <see cref="IdsEvaluator"/> carries them into the report.</summary>
public sealed record IdsDocument(Xids Ids, IReadOnlyList<string> Warnings);

/// <summary>Reads a buildingSMART IDS 1.0 file with Xbim.InformationSpecifications, the parser the
/// reuse check chose (bim-open-toolkit, docs/proposals/ids-reuse.md).</summary>
public static class IdsFile
{
    public static IdsDocument Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("IDS file not found", path);
        var log = new WarningLog();
        var ids = Xids.LoadBuildingSmartIDS(path, log) ?? throw new InvalidDataException($"Not a readable IDS file: {path}; {log.Summary}");
        return new(ids, log.Messages);
    }

    public static IdsDocument Load(Stream stream)
    {
        var log = new WarningLog();
        var ids = Xids.LoadBuildingSmartIDS(stream, log) ?? throw new InvalidDataException($"Not a readable IDS stream; {log.Summary}");
        return new(ids, log.Messages);
    }

    /// <summary>Parses IDS XML held in a string.</summary>
    public static IdsDocument Parse(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return Load(stream);
    }

    /// <summary>Collects the loader's warnings and errors as text, one line each.</summary>
    private sealed class WarningLog : ILogger
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public string Summary => _messages.Count == 0 ? "no loader message" : string.Join("; ", _messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _messages.Add($"IDS loader: {formatter(state, exception)}");
        }
    }
}
