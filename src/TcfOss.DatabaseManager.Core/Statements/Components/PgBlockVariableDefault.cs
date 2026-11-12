using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgBlockVariableDefault(PgInitializerOperator Op, Expression Value) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Op} {Value}");
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Op.ToSql(writer);
        writer.Write(" ");
        Value.FormatSql(writer, manager);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Value.GetReferencedItems(context);
    }
}
