using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public record Comment(string Text) : IWriteSql
{
    public virtual void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"COMMENT '{Text}'");
    }

    public record WithEqual(string Text) : Comment(Text)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"COMMENT = '{Text}'");
        }
    }
}
