using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

/// <summary>
/// Optional T-SQL <c>WITH</c> options block for <c>CREATE VIEW</c>:
/// <c>ENCRYPTION</c>, <c>SCHEMABINDING</c>. Not used by MySQL/MariaDB.
/// </summary>
public record MsViewWithOptions : IWriteSql
{
    public bool Encryption { get; init; }
    public bool SchemaBinding { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("WITH");
        bool needsComma = false;
        if (Encryption)
        {
            writer.Write(" ENCRYPTION");
            needsComma = true;
        }
        if (SchemaBinding)
        {
            if (needsComma)
            {
                writer.Write(",");
            }
            writer.Write(" SCHEMABINDING");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }
}
