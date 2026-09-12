using TcfOss.DatabaseManager.Core.Expressions.Attributes;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record MatchAgainst(SqlValueList<Expression> Columns, Expression MatchExpression, MatchAgainstModifier? Modifier) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"MATCH ({Columns}) AGAINST ({MatchExpression}");
        if (Modifier != null)
        {
            writer.WriteSql($" {Modifier}");
        }
        writer.Write(")");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Expression column in Columns)
        {
            foreach (ItemRef item in column.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        foreach (ItemRef item in MatchExpression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
