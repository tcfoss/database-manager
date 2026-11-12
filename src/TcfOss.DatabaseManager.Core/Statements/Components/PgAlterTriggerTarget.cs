using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgAlterTriggerTarget : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record Named(Identifier Name) : PgAlterTriggerTarget
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

    public record All : PgAlterTriggerTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("ALL");
        }
    }

    public record User : PgAlterTriggerTarget
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("USER");
        }
    }
}
