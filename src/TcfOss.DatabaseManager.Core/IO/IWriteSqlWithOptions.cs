namespace TcfOss.DatabaseManager.Core.IO;

public interface IWriteSqlWithOptions : IWriteSql
{
    void ToSql(SqlTextWriter writer, WriteOptions options);
}
