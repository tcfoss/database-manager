using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgPartitionKey(Expression Expression, ObjectName? OperatorClass = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        Expression.ToSql(writer);
        if (OperatorClass != null)
        {
            writer.WriteSql($" {OperatorClass}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Expression.FormatSql(writer, manager);
        if (OperatorClass != null)
        {
            writer.Write(" ");
            OperatorClass.FormatSql(writer, manager);
        }
    }
}
