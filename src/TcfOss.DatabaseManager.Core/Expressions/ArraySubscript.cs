using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record ArraySubscript(Expression Array, SqlValueList<ArraySubscriptDimension> Dimensions) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        Array.ToSql(writer);
        foreach (ArraySubscriptDimension dimension in Dimensions)
        {
            writer.WriteSql($"[{dimension}]");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Array.FormatSql(writer, manager);
        foreach (ArraySubscriptDimension dimension in Dimensions)
        {
            writer.Write("[");
            dimension.FormatSql(writer, manager);
            writer.Write("]");
        }
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Array.GetReferencedItems(context))
        {
            yield return item;
        }

        foreach (ArraySubscriptDimension dimension in Dimensions)
        {
            foreach (ItemRef item in dimension.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
