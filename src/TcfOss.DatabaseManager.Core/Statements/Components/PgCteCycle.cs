using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCteCycle(SqlValueList<Identifier> Columns, Identifier MarkColumn, Identifier PathColumn) : IWriteSql
{
    public PgCteCycleMark? Mark { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"CYCLE {Columns} SET {MarkColumn}");
        if (Mark != null)
        {
            writer.WriteSql($" {Mark}");
        }
        writer.WriteSql($" USING {PathColumn}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write("CYCLE ");
        writer.WriteDelimited(Columns.Select(manager.GetQuotedIdentifier), ", ");
        writer.WriteSql($" SET {manager.GetQuotedIdentifier(MarkColumn)}");
        if (Mark != null)
        {
            writer.Write(" ");
            Mark.FormatSql(writer, manager);
        }
        writer.WriteSql($" USING {manager.GetQuotedIdentifier(PathColumn)}");
    }
}
