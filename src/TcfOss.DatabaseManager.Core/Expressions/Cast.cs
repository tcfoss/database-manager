using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record Cast(Expression Expression, DataTypeReference DataType) : Expression
{
    public bool UsePostgresCastSyntax { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (UsePostgresCastSyntax)
        {
            writer.WriteSql($"{Expression}::{DataType}");
            return;
        }
        writer.WriteSql($"CAST({Expression} AS {DataType})");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
