using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgStatementTriggerArgument : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record Literal(Value Value) : PgStatementTriggerArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Value.ToSql(writer);
        }
    }

    public record Named(Identifier Name) : PgStatementTriggerArgument
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);
        }
    }
}
