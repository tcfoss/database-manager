using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Configuration.Attributes;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Attributes;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public abstract record StatementColumn(Identifier Name, SqlValueList<StatementColumnOption> Options) : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSqlBody(SqlTextWriter writer, FormatManager manager);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Typed(Identifier Name, DataType DataType, SqlValueList<StatementColumnOption> Options)
        : StatementColumn(Name, Options)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} {DataType}");
            if (Options.Any())
            {
                writer.WriteSql($" {Options.ToSqlDelimited(" ")}");
            }
        }

        public override void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.WriteSql($" {DataType}");
            if (Options.Any())
            {
                foreach (StatementColumnOption option in Options)
                {
                    writer.Write(" ");
                    option.FormatSql(writer, manager);
                }
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }

    public record MsComputed(Identifier Name, Expression Expression, SqlValueList<StatementColumnOption> Options)
        : StatementColumn(Name, Options)
    {
        public MsComputedPersistenceMode? PersistenceMode { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"{Name} AS {Expression}");
            if (PersistenceMode is not null)
            {
                writer.WriteSql($" {PersistenceMode}");
            }
            if (Options.Any())
            {
                writer.WriteSql($" {Options.ToSqlDelimited(" ")}");
            }
        }

        public override void FormatSqlBody(SqlTextWriter writer, FormatManager manager)
        {
            Name.FormatSql(writer, manager);
            writer.Write(" AS ");
            Expression expression = Expression;
            if (expression is Nested nested
                && manager.Formatting.ComputedColumnParentheseHandling == MsComputedColumnParentheseHandling.Never)
            {
                expression = nested.Expression;
            }
            else if (expression is not Nested
                     && manager.Formatting.ComputedColumnParentheseHandling == MsComputedColumnParentheseHandling.Always)
            {
                expression = new Nested(expression);
            }
            expression.FormatSql(writer, manager);

            if (PersistenceMode is not null)
            {
                writer.WriteSql($" {PersistenceMode}");
            }
            if (Options.Any())
            {
                foreach (StatementColumnOption option in Options)
                {
                    writer.Write(" ");
                    option.FormatSql(writer, manager);
                }
            }
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(manager.Indent);
            FormatSqlBody(writer, manager);
        }
    }
}
