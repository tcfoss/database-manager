using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Common;

public record Definer(Account Account) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DEFINER = {Account}");
    }
}
