using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

#pragma warning disable CA1716 // Identifiers should not match keywords
public record Call(Expression Expression) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"CALL {Expression}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSql($"{manager.Indent}CALL ");
        if (Expression is FunctionCall invocation)
        {
            invocation.Name.FormatSql(writer, manager);
            invocation.Arguments.FormatSql(writer, manager);
        }
        else
        {
            Expression.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (Expression is FunctionCall invocation)
        {
            ItemRef? procedure = context.CreateProcedureRef(invocation.Name);
            if (procedure != null)
            {
                yield return procedure;
            }
            foreach (ItemRef item in invocation.Arguments.GetReferencedItems(context))
            {
                yield return item;
            }
            yield break;
        }
        foreach (ItemRef item in Expression.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
