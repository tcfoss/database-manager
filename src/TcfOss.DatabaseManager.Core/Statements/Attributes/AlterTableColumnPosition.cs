using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Attributes;

public abstract record AlterTableColumnPosition() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);

    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        ToSql(writer);
    }

    public record AfterColumn(Identifier Column) : AlterTableColumnPosition
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write($"AFTER {Column}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("AFTER ");
            Column.FormatSql(writer, manager);
        }
    }

    public record First() : AlterTableColumnPosition
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("FIRST");
        }
    }
}
