using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

#pragma warning disable CA1716 // Identifiers should not match keywords

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Case(SqlValueList<Expression> Conditions, SqlValueList<Expression> Results) : Expression
{
    public Expression? Operand { get; init; }
    public Expression? ElseResult { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("CASE");

        if (Operand != null)
        {
            writer.WriteSql($" {Operand}");
        }

        if (Conditions.SafeAny())
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                writer.WriteSql($" WHEN {Conditions[i]} THEN {Results[i]}");
            }
        }

        if (ElseResult != null)
        {
            writer.WriteSql($" ELSE {ElseResult}");
        }

        writer.Write(" END");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Operand != null)
        {
            foreach (ItemRef item in Operand.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        foreach (Expression condition in Conditions)
        {
            foreach (ItemRef item in condition.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        foreach (Expression result in Results)
        {
            foreach (ItemRef item in result.GetReferencedItems(context))
            {
                yield return item;
            }
        }

        if (ElseResult != null)
        {
            foreach (ItemRef item in ElseResult.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
