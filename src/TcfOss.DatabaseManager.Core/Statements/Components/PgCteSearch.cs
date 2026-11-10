using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgCteSearch(PgCteSearchOrder Order, SqlValueList<Identifier> Columns, Identifier SequenceColumn) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"SEARCH {Order} BY {Columns} SET {SequenceColumn}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"SEARCH {Order} BY ");
        writer.WriteDelimited(Columns.Select(manager.GetQuotedIdentifier), ", ");
        writer.WriteSql($" SET {manager.GetQuotedIdentifier(SequenceColumn)}");
    }
}
