using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record MsRoutineParameterOptions(bool Varying = false, bool? Nullable = null, OutputKeyword? Output = null, bool ReadOnly = false) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        WriteBeforeDefault(writer);
        WriteAfterDefault(writer);
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public void WriteBeforeDefault(SqlTextWriter writer)
    {
        if (Varying)
        {
            writer.Write(" VARYING");
        }
        if (Nullable is bool nullable)
        {
            writer.Write(nullable ? " NULL" : " NOT NULL");
        }
    }

    public void WriteAfterDefault(SqlTextWriter writer)
    {
        if (Output != null)
        {
            writer.Write($" {Output}");
        }
        if (ReadOnly)
        {
            writer.Write(" READONLY");
        }
    }
}
