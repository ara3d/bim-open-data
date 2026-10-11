#nullable enable
using System;

namespace Ara3D.BimOpenSchema;

/// <summary>
/// Row indices of a table grouped by an integer key in [0, keyCount), built by one counting
/// sort: O(rows + keys) time, two int arrays of memory. Rows keep their table order inside a
/// group. Rows whose key is out of range belong to no group. <see cref="BosScene"/> uses it to
/// group instances, parameters, and relations by entity.
/// </summary>
internal sealed class RowGroups
{
    private readonly int[] _offsets;
    private readonly int[] _rows;

    public RowGroups(int keyCount, int rowCount, Func<int, int> keyOfRow)
    {
        _offsets = new int[keyCount + 1];
        for (var row = 0; row < rowCount; row++)
        {
            var key = keyOfRow(row);
            if ((uint)key < (uint)keyCount)
                _offsets[key + 1]++;
        }
        for (var key = 0; key < keyCount; key++)
            _offsets[key + 1] += _offsets[key];

        _rows = new int[_offsets[keyCount]];
        var next = _offsets[..keyCount];
        for (var row = 0; row < rowCount; row++)
        {
            var key = keyOfRow(row);
            if ((uint)key < (uint)keyCount)
                _rows[next[key]++] = row;
        }
    }

    public int KeyCount => _offsets.Length - 1;

    /// <summary>The rows whose key is <paramref name="key"/>, in table order; empty for a key
    /// out of range.</summary>
    public ReadOnlySpan<int> Rows(int key)
        => (uint)key < (uint)KeyCount
            ? _rows.AsSpan(_offsets[key], _offsets[key + 1] - _offsets[key])
            : ReadOnlySpan<int>.Empty;
}
