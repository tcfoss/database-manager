using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record PgRoutineOptions(Identifier Language) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"LANGUAGE {Language}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteSqlI("LANGUAGE ");
        Language.FormatSql(writer, manager);
    }
}
