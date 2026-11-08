using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public record PgIntervalQualifier(DateTimeUnit StartField, DateTimeUnit? EndField = null, uint? Precision = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{StartField}");
        if (EndField != null)
        {
            writer.WriteSql($" TO {EndField}");
        }
        if (Precision != null)
        {
            writer.WriteSql($"({Precision})");
        }
    }
}
