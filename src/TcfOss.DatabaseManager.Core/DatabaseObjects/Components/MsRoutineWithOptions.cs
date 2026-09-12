using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record MsRoutineWithOptions : IWriteSql
{
    public bool Encryption { get; init; }
    public bool Recompile { get; init; }
    public ExecuteAsClause? ExecuteAs { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("WITH");
        if (Encryption)
        {
            writer.Write(" ENCRYPTION");
        }

        if (Recompile)
        {
            if (Encryption)
            {
                writer.Write(",");
            }

            writer.Write(" RECOMPILE");
        }

        if (ExecuteAs != null)
        {
            if (Encryption || Recompile)
            {
                writer.Write(",");
            }

            writer.WriteSql($" {ExecuteAs}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }
}
