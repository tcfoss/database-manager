using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record PgStatementBaseTypeOption(PgBaseTypeOptionName Name) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Named(PgBaseTypeOptionName Name, ObjectName Value) : PgStatementBaseTypeOption(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} = {Value}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            // Bare option values can be type keywords; quoting them can change their meaning.
            ToSql(writer);
        }
    }

    public record TypeReference(PgBaseTypeOptionName Name, DataTypeReference Value) : PgStatementBaseTypeOption(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} = {Value}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.WriteSql($"{Name} = ");
            Value.FormatSql(writer, manager);
        }
    }

    public record Literal(PgBaseTypeOptionName Name, Value Value) : PgStatementBaseTypeOption(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} = {Value}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record Flag(PgBaseTypeOptionName Name) : PgStatementBaseTypeOption(Name)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name}");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }
}
