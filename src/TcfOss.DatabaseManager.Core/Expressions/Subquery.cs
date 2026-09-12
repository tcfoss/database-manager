using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Subquery(Select Query) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"({Query})");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        writer.WriteLine("(");
        manager.IncreaseIndent();
        Query.FormatSql(writer, manager);
        writer.Write(")");
        manager.DecreaseIndent();
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Query.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
