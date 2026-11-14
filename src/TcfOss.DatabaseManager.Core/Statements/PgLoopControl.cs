using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

public abstract record PgLoopControl(Identifier? Label, Expression? Condition) : Statement
{
    protected abstract string Keyword { get; }

    public record Exit(Identifier? Label, Expression? Condition) : PgLoopControl(Label, Condition)
    {
        protected override string Keyword => "EXIT";
    }

    public record Continue(Identifier? Label, Expression? Condition) : PgLoopControl(Label, Condition)
    {
        protected override string Keyword => "CONTINUE";
    }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write(Keyword);
        if (Label != null)
        {
            writer.WriteSql($" {Label}");
        }
        if (Condition != null)
        {
            writer.WriteSql($" WHEN {Condition}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, Keyword);
        if (Label != null)
        {
            writer.Write(" ");
            Label.FormatSql(writer, manager);
        }
        if (Condition != null)
        {
            writer.Write(" WHEN ");
            Condition.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Condition != null)
        {
            foreach (ItemRef item in Condition.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
