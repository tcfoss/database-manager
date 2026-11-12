using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects.Components;

public abstract record PgRoutineSetting(ObjectName Name) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record FromCurrent(ObjectName Name) : PgRoutineSetting(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET {Name} FROM CURRENT");
        }
    }

    public record ToDefault(ObjectName Name) : PgRoutineSetting(Name)
    {
        public SettingAssignmentOperator Op { get; init; } = SettingAssignmentOperator.To;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET {Name} {Op} DEFAULT");
        }
    }

    public record ToValues(ObjectName Name, SqlValueList<Value> Values) : PgRoutineSetting(Name)
    {
        public SettingAssignmentOperator Op { get; init; } = SettingAssignmentOperator.To;

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET {Name} {Op} {Values.ToSqlDelimited()}");
        }
    }
}
