using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record StatementColumn(Identifier Name, DataType DataType, SqlValueList<StatementColumnOption> Options) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name} {DataType}");
        if (Options.Any())
        {
            writer.WriteSql($" {Options.ToSqlDelimited(" ")}");
        }
    }

    public void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        writer.WriteSql($" {DataType}");
        if (Options.Any())
        {
            foreach (StatementColumnOption option in Options)
            {
                writer.Write(" ");
                option.FormatSql(writer, manager);
            }
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.Indent);
        FormatSqlBody(writer, manager);
    }
}
