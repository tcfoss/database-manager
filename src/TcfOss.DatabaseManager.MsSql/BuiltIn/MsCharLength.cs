using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MsSql.BuiltIn;

public abstract record MsCharLength() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record Specified(uint Length) : MsCharLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Length})");
        }
    }

    public record Max() : MsCharLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("(MAX)");
        }
    }
}
