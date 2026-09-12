using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record SelectInto() : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public virtual void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.Write(manager.Indent);
        ToSql(writer);
    }

    public record SelectIntoTable(ObjectName Name) : SelectInto
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"INTO {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI("INTO ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SelectIntoVariables(SqlValueList<Identifier> Variables) : SelectInto
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"INTO {Variables}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSqlI("INTO ");
            for (int i = 0; i < Variables.Count; i++)
            {
                Variables[i].FormatSql(writer, manager);
                if (i < Variables.Count - 1)
                {
                    writer.Write(", ");
                }
            }
        }
    }

    public record SelectIntoFile(string FilePath) : SelectInto
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"INTO OUTFILE '{FilePath}'");
        }
    }

}
