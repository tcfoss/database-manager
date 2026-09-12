using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record SignalConditionValue() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public record SqlState(string State) : SignalConditionValue
    {
        public bool IncludeValueKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? value = IncludeValueKeyword ? " VALUE" : null;
            writer.WriteSql($"SQLSTATE{value} '{State}'");
        }
    }

    public record ConditionName(string Name) : SignalConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}");
        }
    }
}
