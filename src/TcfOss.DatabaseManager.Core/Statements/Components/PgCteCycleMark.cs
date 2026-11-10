using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCteCycleMark(Expression Value, Expression DefaultValue) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"TO {Value} DEFAULT {DefaultValue}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("TO ");
        Value.FormatSql(writer, manager);
        writer.Write(" DEFAULT ");
        DefaultValue.FormatSql(writer, manager);
    }
}
