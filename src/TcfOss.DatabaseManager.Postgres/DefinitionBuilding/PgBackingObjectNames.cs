using System.Globalization;
using System.Text;
using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Postgres.DefinitionBuilding;

public static class PgBackingObjectNames
{
    public static ObjectIdentifier GetIdentitySequenceName(ColumnIdentifier column, int suffix = 0)
    {
        string label = suffix == 0 ? "seq" : "seq" + suffix.ToString(CultureInfo.InvariantCulture);
        string table = column.Table.Name;
        string name = column.Name;
        int tableBytes = Encoding.UTF8.GetByteCount(table);
        int columnBytes = Encoding.UTF8.GetByteCount(name);
        int available = 63 - Encoding.UTF8.GetByteCount(label) - 2;
        while (tableBytes + columnBytes > available)
        {
            if (tableBytes > columnBytes)
            {
                tableBytes--;
            }
            else
            {
                columnBytes--;
            }
        }
        return new ObjectIdentifier($"{Clip(table, tableBytes)}_{Clip(name, columnBytes)}_{label}", column.Table.Schema, QuoteStyle.Ansi);
    }

    private static string Clip(string value, int bytes)
    {
        int length = 0;
        foreach (Rune rune in value.EnumerateRunes())
        {
            if (bytes < rune.Utf8SequenceLength)
            {
                break;
            }
            bytes -= rune.Utf8SequenceLength;
            length += rune.Utf16SequenceLength;
        }
        return value[..length];
    }
}
