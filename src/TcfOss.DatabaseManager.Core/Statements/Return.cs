using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Statements;

public record Return(Expression? Body) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        if (Body is null)
        {
            writer.Write("RETURN");
            return;
        }
        writer.WriteSql($"RETURN {Body}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        if (Body is null)
        {
            writer.WriteSqlI("RETURN");
        }
        else
        {
            writer.WriteSqlI("RETURN ");
            Body.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Body is null)
        {
            yield break;
        }
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
