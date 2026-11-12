using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgBlockOptions(
    SqlValueList<Statement>? Declarations,
    SqlValueList<PgExceptionHandler>? Handlers,
    Identifier? EndLabel);

public record PgExceptionHandler(SqlValueList<PgExceptionCondition> Conditions, SqlValueList<Statement> Statements) : IWriteSql, IHaveMeta
{
    public MetaData Meta { get; init; } = new();

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("WHEN ");
        writer.WriteDelimited(Conditions, " OR ");
        writer.Write(" THEN ");
        writer.WriteTerminated(Statements.NonInert());
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "WHEN ");
        writer.FormatDelimited(Conditions, manager, " OR ");
        writer.Write(" THEN");
        manager.IncreaseIndent();
        foreach (Statement statement in Statements)
        {
            statement.FormatSql(writer, manager);
            manager.TerminateStatement(writer, statement);
        }
        manager.DecreaseIndent();
    }
}

public abstract record PgExceptionCondition : IWriteSql
{
    public abstract void ToSql(SqlTextWriter writer);
    public abstract void FormatSql(SqlTextWriter writer, FormatManager manager);

    public record Named(Identifier Name) : PgExceptionCondition
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

    public record SqlState(string Value) : PgExceptionCondition
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("SQLSTATE ");
            new Value.SingleQuotedString(Value).ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }

    public record Others : PgExceptionCondition
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.Write("OTHERS");
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            ToSql(writer);
        }
    }
}
