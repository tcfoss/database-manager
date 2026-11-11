using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgPartitionBy(PgPartitionStrategy Strategy, SqlValueList<PgPartitionKey> Keys) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"PARTITION BY {Strategy} ({Keys})");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSql($"PARTITION BY {Strategy} (");
        writer.FormatDelimited(Keys, manager);
        writer.Write(")");
    }
}
