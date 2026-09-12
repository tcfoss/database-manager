using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

public abstract record ConditionValue() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record ErrorCode(uint Value) : ConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Value);
        }
    }

    public record SqlState(string SqlStateValue) : ConditionValue
    {
        public bool IncludeValueKeyword { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            string? value = IncludeValueKeyword ? " VALUE" : null;
            writer.Write($"SQLSTATE{value} '{SqlStateValue}'");
        }
    }

    public record ConditionName(Identifier Name) : ConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            Name.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
        }
    }

    public record SqlWarning() : ConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SQLWARNING");
        }
    }


    public record SqlException() : ConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SQLEXCEPTION");
        }
    }

    public record NotFound() : ConditionValue
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("NOT FOUND");
        }
    }
}
