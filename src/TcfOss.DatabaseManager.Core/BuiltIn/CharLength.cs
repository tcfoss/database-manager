using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public abstract record CharLength() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record Specified(uint Length) : CharLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Length})");
        }
    }

    public record Max() : CharLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("(MAX)");
        }
    }
}
