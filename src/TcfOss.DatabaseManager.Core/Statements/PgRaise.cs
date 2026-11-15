using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record PgRaise(PgRaiseLevel? Level, SqlValueList<PgRaiseOption>? Options) : Statement
{
    protected abstract void WriteBody(SqlTextWriter writer);
    protected abstract void FormatBody(SqlTextWriter writer, FormatManager manager);

    public sealed override void ToSql(SqlTextWriter writer)
    {
        writer.Write("RAISE");
        if (Level is not null)
        {
            writer.WriteSql($" {Level}");
        }
        WriteBody(writer);
    }

    public sealed override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("RAISE");
        if (Level is not null)
        {
            writer.WriteSql($" {Level}");
        }
        FormatBody(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Options is { Count: > 0 })
        {
            foreach (PgRaiseOption option in Options)
            {
                foreach (ItemRef item in option.Value.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }

    protected static void WriteOptions(SqlTextWriter writer, SqlValueList<PgRaiseOption>? options)
    {
        if (options is { Count: > 0 })
        {
            writer.Write(" USING ");
            writer.WriteDelimited(options);
        }
    }

    protected static void FormatOptions(SqlTextWriter writer, FormatManager manager, SqlValueList<PgRaiseOption>? options)
    {
        if (options is not { Count: > 0 })
        {
            return;
        }
        writer.Write(" USING ");
        for (int i = 0; i < options.Count; i++)
        {
            if (i > 0)
            {
                writer.Write(", ");
            }
            options[i].FormatSql(writer, manager);
        }
    }

    public sealed record Rethrow()
        : PgRaise(null, null)
    {
        protected override void WriteBody(SqlTextWriter writer) { }
        protected override void FormatBody(SqlTextWriter writer, FormatManager manager) { }
    }

    public sealed record Message(
        PgRaiseLevel? RaiseLevel,
        Value.SingleQuotedString FormatString,
        SqlValueList<Expression>? Parameters,
        SqlValueList<PgRaiseOption>? Options)
        : PgRaise(RaiseLevel, Options)
    {
        protected override void WriteBody(SqlTextWriter writer)
        {
            writer.Write(" ");
            FormatString.ToSql(writer);
            if (Parameters is { Count: > 0 })
            {
                writer.Write(", ");
                writer.WriteDelimited(Parameters);
            }
            WriteOptions(writer, Options);
        }

        protected override void FormatBody(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(" ");
            FormatString.ToSql(writer);
            if (Parameters is { Count: > 0 })
            {
                writer.Write(", ");
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(", ");
                    }
                    Parameters[i].FormatSql(writer, manager);
                }
            }
            FormatOptions(writer, manager, Options);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            if (Parameters is not null)
            {
                foreach (Expression parameter in Parameters)
                {
                    foreach (ItemRef itemRef in parameter.GetReferencedItems(context))
                    {
                        yield return itemRef;
                    }
                }
            }
            foreach (ItemRef item in base.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    public sealed record ConditionName(
        PgRaiseLevel? RaiseLevel,
        Identifier Name,
        SqlValueList<PgRaiseOption>? Options)
        : PgRaise(RaiseLevel, Options)
    {
        protected override void WriteBody(SqlTextWriter writer)
        {
            writer.Write(" ");
            Name.ToSql(writer);
            WriteOptions(writer, Options);
        }

        protected override void FormatBody(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(" ");
            Name.FormatSql(writer, manager);
            FormatOptions(writer, manager, Options);
        }
    }

    public sealed record SqlState(
        PgRaiseLevel? RaiseLevel,
        Value.SingleQuotedString State,
        SqlValueList<PgRaiseOption>? Options)
        : PgRaise(RaiseLevel, Options)
    {
        protected override void WriteBody(SqlTextWriter writer)
        {
            writer.Write(" SQLSTATE ");
            State.ToSql(writer);
            WriteOptions(writer, Options);
        }

        protected override void FormatBody(SqlTextWriter writer, FormatManager manager)
        {
            writer.Write(" SQLSTATE ");
            State.ToSql(writer);
            FormatOptions(writer, manager, Options);
        }
    }

    public sealed record WithOptions(
        PgRaiseLevel? RaiseLevel,
        SqlValueList<PgRaiseOption> Options)
        : PgRaise(RaiseLevel, Options)
    {
        protected override void WriteBody(SqlTextWriter writer) => WriteOptions(writer, Options);
        protected override void FormatBody(SqlTextWriter writer, FormatManager manager) => FormatOptions(writer, manager, Options);
    }
}
