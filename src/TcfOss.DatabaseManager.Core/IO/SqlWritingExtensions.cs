using System.Text;
using TcfOss.DataStructures.Enums;

namespace TcfOss.DatabaseManager.Core.IO;

public static class SqlWritingExtensions
{
    public static string ToSql(this IWriteSql sql)
    {
        StringBuilder builder = StringBuilderPool.Get();
        using (var writer = new SqlTextWriter(builder))
        {
            sql.ToSql(writer);
        }

        return StringBuilderPool.Return(builder);
    }

    public static string ToSql(this IWriteSql sql, WriteOptions options)
    {
        StringBuilder builder = StringBuilderPool.Get();
        using (var writer = new SqlTextWriter(builder))
        {
            sql.ToSql(writer, options);
        }

        return StringBuilderPool.Return(builder);
    }

    public static void ToSql<T>(this IStringEnum<T>? enumLike, SqlTextWriter writer)
    {
        writer.Write(enumLike?.ToString()!);
    }

    public static string ToSql<T>(this IStringEnum<T>? enumLike)
    {
        return enumLike?.ToString() ?? "";
    }

    public static string ToSqlDelimited<T>(this IEnumerable<T>? list, string delimiter = ", ") where T : IWriteSql
    {
        if (list == null)
        {
            return "";
        }

        StringBuilder builder = StringBuilderPool.Get();

        using (var writer = new SqlTextWriter(builder))
        {
            writer.WriteDelimited(list, delimiter);
        }

        return StringBuilderPool.Return(builder);
    }
}
