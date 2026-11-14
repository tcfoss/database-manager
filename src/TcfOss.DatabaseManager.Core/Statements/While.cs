using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record While(Expression Condition) : Statement
{
    public record My(Expression Condition, SqlValueList<Statement> Statements) : While(Condition)
    {
        public Identifier? EndLabel { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteBlocked(writer, Statements, "DO", "WHILE", EndLabel);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatBlocked(writer, manager, Statements, "DO", "WHILE", EndLabel);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return GetBlockedReferencedItems(context, Statements);
        }
    }

    public record Pg(Expression Condition, SqlValueList<Statement> Statements) : While(Condition)
    {
        public Identifier? EndLabel { get; init; }

        public override void ToSql(SqlTextWriter writer)
        {
            WriteBlocked(writer, Statements, "LOOP", "LOOP", EndLabel);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            FormatBlocked(writer, manager, Statements, "LOOP", "LOOP", EndLabel);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            return GetBlockedReferencedItems(context, Statements);
        }
    }

    public record Ms(Expression Condition, Statement Body) : While(Condition)
    {
        public override void ToSql(SqlTextWriter writer)
        {
            writer.WriteSql($"WHILE {Condition} ");
            Body.ToSql(writer);
        }

        public override void FormatSql(SqlTextWriter writer, FormatManager manager)
        {
            Meta.FormatPreNonSql(writer, manager);
            manager.WriteBlockPart(writer, "WHILE");
            writer.Write(" ");
            Condition.FormatSql(writer, manager);
            bool indentBody = Body is not BeginEnd;
            if (indentBody)
            {
                manager.IncreaseIndent();
            }
            Body.FormatSql(writer, manager);
            if (indentBody)
            {
                manager.DecreaseIndent();
            }
            Meta.FormatPostNonSql(writer, manager);
        }

        public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
        {
            foreach (ItemRef item in Condition.GetReferencedItems(context))
            {
                yield return item;
            }
            foreach (ItemRef item in Body.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }

    protected void WriteBlocked(SqlTextWriter writer, SqlValueList<Statement> statements, string start, string end, Identifier? endLabel)
    {
        writer.WriteSql($"WHILE {Condition} {start} ");
        writer.WriteTerminated(statements.NonInert());
        writer.WriteSql($"END {end}");
        if (endLabel != null)
        {
            writer.WriteSql($" {endLabel}");
        }
    }

    protected void FormatBlocked(SqlTextWriter writer, FormatManager manager, SqlValueList<Statement> statements, string start, string end, Identifier? endLabel)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "WHILE");
        writer.Write(" ");
        Condition.FormatSql(writer, manager);
        writer.WriteSql($" {start}");
        manager.IncreaseIndent();
        foreach (Statement statement in statements)
        {
            statement.FormatSql(writer, manager);
            manager.TerminateStatement(writer, statement);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, $"END {end}");
        if (endLabel != null)
        {
            writer.Write(" ");
            endLabel.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    protected IEnumerable<ItemRef> GetBlockedReferencedItems(ReferencedItemsManager context, SqlValueList<Statement> statements)
    {
        foreach (ItemRef item in Condition.GetReferencedItems(context))
        {
            yield return item;
        }
        foreach (Statement statement in statements)
        {
            foreach (ItemRef item in statement.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
