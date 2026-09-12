using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.BuiltIn;

public abstract record NumericLength() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record Precision(uint Length) : NumericLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Length})");
        }
    }

    public record PrecisionScale(uint Length, uint Scale) : NumericLength
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"({Length},{Scale})");
        }
    }
}
