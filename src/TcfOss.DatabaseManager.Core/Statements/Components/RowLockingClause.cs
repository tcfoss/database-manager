using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record RowLockingClause(RowLockStrength Strength) : IWriteSql
{
    public SqlValueList<Identifier>? Of { get; init; }
    public RowLockWait? Wait { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"FOR {Strength}");
        if (Of != null)
        {
            writer.WriteSql($" OF {Of.ToSqlDelimited()}");
        }
        if (Wait != null)
        {
            writer.WriteSql($" {Wait}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"{manager.Indent}FOR {Strength}");
        if (Of != null)
        {
            writer.Write(" OF ");
            writer.FormatDelimited(Of, manager);
        }
        if (Wait != null)
        {
            writer.WriteSql($" {Wait}");
        }
    }
}
