using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record IndexStorageLocation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record Filegroup(Identifier Name) : IndexStorageLocation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON {Name}");
        }
    }

    public record PartitionScheme(Identifier Name, Identifier Column) : IndexStorageLocation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON {Name} ({Column})");
        }
    }
}
