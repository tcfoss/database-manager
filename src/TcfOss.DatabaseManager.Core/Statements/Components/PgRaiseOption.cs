using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgRaiseOption(PgRaiseOptionName Name, PgAssignmentOperator Op, Expression Value) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name} {Op} {Value}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.ToSql(writer);
        writer.WriteSql($" {Op} ");
        Value.FormatSql(writer, manager);
    }
}
