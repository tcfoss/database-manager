using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record ViewCheckOption : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record Specified(ViewCheckOptionScope Scope) : ViewCheckOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"WITH {Scope} CHECK OPTION");
        }
    }

    public record Unspecified : ViewCheckOption
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("WITH CHECK OPTION");
        }
    }
}
