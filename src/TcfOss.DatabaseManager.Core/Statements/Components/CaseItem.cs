using TcfOss.DatabaseManager.Core.Expressions;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements.Components;

public record class CaseItem(SqlValueList<Expression> Conditions, SqlValueList<Statement> Statements)
    : IWriteSql, IHaveMeta
{
    public MetaData Meta { get; set; } = new();

    public void ToSql(SqlTextWriter writer)
    {
        writer.Write("WHEN ");
        writer.Write(Conditions.ToSqlDelimited(", "));
        writer.Write(" THEN ");
        writer.WriteTerminated(Statements.NonInert());
    }

    public void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "WHEN ");
        writer.FormatDelimited(Conditions, manager);
        writer.Write(" THEN");
        manager.IncreaseIndent();
        foreach (Statement statement in Statements)
        {
            statement.FormatSql(writer, manager);
            manager.TerminateStatement(writer, statement);
        }
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Expression condition in Conditions)
        {
            foreach (ItemRef item in condition.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        foreach (Statement statement in Statements)
        {
            foreach (ItemRef item in statement.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
