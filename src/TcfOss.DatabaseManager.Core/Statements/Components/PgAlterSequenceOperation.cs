using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgAlterSequenceOperation : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Parameters(SqlValueList<PgStatementSequenceOption> Options) : PgAlterSequenceOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteDelimited(Options, " ");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            for (int index = 0; index < Options.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(" ");
                }
                Options[index].FormatSql(writer, manager);
            }
        }
    }

    public record OwnerTo(Account Owner) : PgAlterSequenceOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"OWNER TO {Owner}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("OWNER TO ");
            Owner.FormatSql(writer, manager);
        }
    }

    public record RenameTo(Identifier Name) : PgAlterSequenceOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"RENAME TO {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("RENAME TO ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SetSchema(Identifier Name) : PgAlterSequenceOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"SET SCHEMA {Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write("SET SCHEMA ");
            Name.FormatSql(writer, manager);
        }
    }

    public record SetPersistence(bool Unlogged) : PgAlterSequenceOperation
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write(Unlogged ? "SET UNLOGGED" : "SET LOGGED");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }
}
