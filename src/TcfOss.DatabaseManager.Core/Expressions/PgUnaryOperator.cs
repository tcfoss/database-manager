using TcfOss.DatabaseManager.Core.Expressions.Components;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record PgUnaryOperator(PgOperatorReference Operator, Expression Expression) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Operator} {Expression}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Operator.FormatSql(writer, manager);
        writer.Write(" ");
        Expression.FormatSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Expression.GetReferencedItems(context);
    }
}
