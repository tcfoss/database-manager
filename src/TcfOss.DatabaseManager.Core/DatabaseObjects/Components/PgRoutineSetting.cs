using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

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

    public record ToValues(ObjectName Name, SqlValueList<Value> Values) : PgRoutineSetting(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET {Name} TO {Values.ToSqlDelimited()}");
        }
    }
}
