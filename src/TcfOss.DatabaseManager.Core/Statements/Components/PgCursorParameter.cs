using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCursorParameter(Identifier Name, DataTypeReference DataType) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name} {DataType}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        writer.Write(" ");
        DataType.FormatSql(writer, manager);
    }
}
