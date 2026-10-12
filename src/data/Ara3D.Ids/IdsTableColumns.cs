using System.Globalization;
using Ara3D.DataTable;

namespace Ara3D.Ids;

/// <summary>Reading the text tables <see cref="IdsModel.FromTables"/> and
/// <see cref="IdsRelations.FromTable"/> take: columns by name, case-insensitively, and cells as
/// an index or as text.</summary>
internal static class IdsTableColumns
{
    public static int Required(IDataTable table, string name)
        => Optional(table, name) is var i and >= 0
            ? i
            : throw new ArgumentException(
                $"Table '{table.Name}' has no '{name}' column; its columns are {string.Join(", ", table.Columns.Select(c => c.Descriptor.Name))}");

    /// <summary>The column's position, or -1 when the table has none by that name.</summary>
    public static int Optional(IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (string.Equals(table.Columns[i].Descriptor.Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>The cell as an entity index, -1 when it is empty.</summary>
    public static long Index(IDataTable table, int column, int row)
        => table[column, row] is { } value and not DBNull ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : -1;

    public static string Text(IDataTable table, int column, int row)
        => table[column, row] is { } value and not DBNull
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            : "";
}
