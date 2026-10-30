using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record CompositeTypeField(Identifier Name, DataTypeReference DataType, ObjectName? Collation = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Name} {DataType}");
        if (Collation != null)
        {
            writer.WriteSql($" COLLATE {Collation}");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Name.FormatSql(writer, manager);
        writer.Write(" ");
        DataType.FormatSql(writer, manager);
        if (Collation != null)
        {
            writer.Write(" COLLATE ");
            Collation.FormatSql(writer, manager);
        }
    }
}
