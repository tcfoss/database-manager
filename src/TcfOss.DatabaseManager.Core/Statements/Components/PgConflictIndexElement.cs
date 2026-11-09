using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record PgConflictIndexElement(Expression Expression, ObjectName? OperatorClass = null) : IWriteSql
{
    public SqlValueList<Assignment>? OperatorClassParameters { get; init; }

    public void ToSql(SqlTextWriter writer)
    {
        Expression.ToSql(writer);
        if (OperatorClass != null)
        {
            writer.WriteSql($" {OperatorClass}");
            if (OperatorClassParameters != null)
            {
                writer.WriteSql($" ({OperatorClassParameters})");
            }
        }
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Expression.FormatSql(writer, manager);
        if (OperatorClass != null)
        {
            writer.Write(" ");
            OperatorClass.FormatSql(writer, manager);
            if (OperatorClassParameters != null)
            {
                writer.Write(" (");
                writer.FormatDelimited(OperatorClassParameters, manager);
                writer.Write(")");
            }
        }
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        return Expression.GetReferencedItems(context);
    }
}
