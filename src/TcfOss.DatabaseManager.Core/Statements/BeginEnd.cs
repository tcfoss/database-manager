using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record BeginEnd(SqlValueList<Statement> Statements) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("BEGIN ");
        writer.WriteTerminated(Statements.NonInert());
        writer.Write("END");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "BEGIN");
        manager.IncreaseIndent();
        foreach (Statement stmt in Statements)
        {
            stmt.FormatSql(writer, manager);
            manager.TerminateStatement(writer, stmt);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, "END");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Statement stmt in Statements)
        {
            foreach (ItemRef item in stmt.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
