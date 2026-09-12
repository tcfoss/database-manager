using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

/// <summary>
/// A T-SQL-style <c>WHILE</c> loop:
/// <c>WHILE &lt;bool-expr&gt; &lt;stmt&gt;</c>. The body is a single
/// statement (typically a <c>BEGIN…END</c> block).
/// </summary>
public record While(Expression Condition, Statement Body) : Statement
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
        writer.Write(" ");
        Body.FormatSql(writer, manager);
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
