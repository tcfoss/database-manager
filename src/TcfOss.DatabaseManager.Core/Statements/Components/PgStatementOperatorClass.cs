using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgStatementOperatorClass(ObjectName Name, SqlValueList<ObjectNameKvp>? Parameters = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        Name.ToSql(writer);
        if (Parameters != null)
        {
            writer.WriteSql($" ({Parameters})");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        if (Parameters != null)
        {
            writer.Write(" (");
            writer.FormatDelimited(Parameters, manager);
            writer.Write(")");
        }
    }
}
