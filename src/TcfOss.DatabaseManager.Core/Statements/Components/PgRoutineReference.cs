using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgRoutineReference(ObjectName Name, SqlValueList<StatementRoutineParameter>? Parameters) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        Name.ToSql(writer);
        if (Parameters != null)
        {
            writer.WriteSql($"({Parameters})");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        if (Parameters != null)
        {
            writer.Write("(");
            for (int index = 0; index < Parameters.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                Parameters[index].FormatSql(writer, manager);
            }
            writer.Write(")");
        }
    }
}

