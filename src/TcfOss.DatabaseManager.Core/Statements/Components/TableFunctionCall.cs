using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record TableFunctionCall(FunctionCall Call, SqlValueList<CompositeTypeField>? Columns = null) : IWriteSql
{
    public void ToSql(SqlTextWriter writer)
    {
        Call.ToSql(writer);
        if (Columns != null)
        {
            writer.WriteSql($" AS ({Columns.ToSqlDelimited()})");
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Call.FormatSql(writer, manager);
        if (Columns != null)
        {
            writer.Write(" AS (");
            for (int index = 0; index < Columns.Count; index++)
            {
                if (index > 0)
                {
                    writer.Write(", ");
                }
                Columns[index].FormatSql(writer, manager);
            }
            writer.Write(")");
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Call.GetReferencedItems(context);
    }
}
