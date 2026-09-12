using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record EventSchedule() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record At(Expression Time) : EventSchedule
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON SCHEDULE AT {Time}");
        }
    }

    public record Every(Expression Quantity, DateTimeUnit Unit) : EventSchedule
    {
        public Expression? Start { get; init; }
        public Expression? End { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"ON SCHEDULE EVERY {Quantity} {Unit}");
            if (Start != null)
            {
                writer.WriteSql($" STARTS {Start}");
            }
            if (End != null)
            {
                writer.WriteSql($" ENDS {End}");
            }
        }
    }
}
