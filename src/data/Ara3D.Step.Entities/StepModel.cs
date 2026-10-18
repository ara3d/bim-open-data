using Ara3D.IO.StepParser;

namespace Ara3D.Step.Entities;

/// <summary>
/// A parsed STEP document with its instances found by id. It does not own the document: dispose the document
/// after the last read. A duplicated id resolves to its first instance.
/// </summary>
public sealed class StepModel
{
    private readonly Dictionary<int, int> _indexById;

    public StepModel(StepDocument document)
    {
        Document = document;
        _indexById = new(document.Definitions.Count);
        for (var i = 0; i < document.Definitions.Count; i++)
            _indexById.TryAdd(document.Definitions[i].Id, i);
    }

    public StepDocument Document { get; }

    public int Count
        => Document.Definitions.Count;

    /// <summary>The instance at a position in file order.</summary>
    public StepEntity EntityAt(int index)
        => new(this, Document.Definitions[index]);

    public StepEntity? Find(int id)
        => _indexById.TryGetValue(id, out var index) ? EntityAt(index) : null;

    public StepEntity Get(int id)
        => Find(id) ?? throw new KeyNotFoundException($"#{id} is not defined in {Document.FilePath}");
}
