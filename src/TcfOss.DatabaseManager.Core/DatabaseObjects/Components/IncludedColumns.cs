using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record IncludedColumns(SqlValueList<Identifier> Columns) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"INCLUDE ({Columns})");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("INCLUDE (");
        writer.FormatDelimited(Columns, manager);
        writer.Write(")");
    }
}
