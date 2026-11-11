using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgConstraintIndexParameters(SqlValueList<PgStorageParameter>? StorageParameters, Identifier? Tablespace) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        if (StorageParameters != null)
        {
            writer.WriteSql($"WITH ({StorageParameters})");
        }
        if (Tablespace != null)
        {
            if (StorageParameters != null)
            {
                writer.Write(" ");
            }
            writer.WriteSql($"USING INDEX TABLESPACE {Tablespace}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        if (StorageParameters != null)
        {
            writer.Write("WITH (");
            writer.FormatDelimited(StorageParameters, manager);
            writer.Write(")");
        }
        if (Tablespace != null)
        {
            if (StorageParameters != null)
            {
                writer.Write(" ");
            }
            writer.Write("USING INDEX TABLESPACE ");
            Tablespace.FormatSql(writer, manager);
        }
    }
}
