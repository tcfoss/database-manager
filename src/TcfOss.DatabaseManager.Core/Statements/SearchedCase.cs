using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record SearchedCase(SqlValueList<CaseItem> Cases, SqlValueList<Statement>? ElseStatements) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("CASE");
        foreach (CaseItem caseItem in Cases)
        {
            writer.WriteSql($" {caseItem}");
        }
        if (ElseStatements != null)
        {
            writer.Write(" ELSE ");
            writer.WriteTerminated(ElseStatements.NonInert());
        }
        writer.Write(" END CASE");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "CASE");
        manager.IncreaseIndent();
        foreach (CaseItem item in Cases)
        {
            item.FormatSql(writer, manager);
        }
        manager.DecreaseIndent();
        if (ElseStatements != null)
        {
            manager.WriteBlockPart(writer, "ELSE");
            manager.IncreaseIndent();
            foreach (Statement statement in ElseStatements)
            {
                statement.FormatSql(writer, manager);
                manager.TerminateStatement(writer, statement);
            }
            manager.DecreaseIndent();
        }
        manager.WriteBlockPart(writer, "END CASE");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (CaseItem caseItem in Cases)
        {
            foreach (ItemRef item in caseItem.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        if (ElseStatements != null)
        {
            foreach (Statement statement in ElseStatements)
            {
                foreach (ItemRef item in statement.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
    }
}
