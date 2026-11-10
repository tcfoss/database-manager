using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgStorageParameter(ObjectName Name, Value? Value = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        Name.ToSql(writer);
        if (Value != null)
        {
            writer.WriteSql($" = {Value}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        if (Value != null)
        {
            writer.WriteSql($" = {Value}");
        }
    }
}
