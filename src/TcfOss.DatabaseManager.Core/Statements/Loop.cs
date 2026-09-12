using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

# pragma warning disable CA1716 // Reserved language keyword 'Loop'

public record Loop(SqlValueList<Statement> Statements) : Statement
{
    public Identifier? EndLabel { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("LOOP ");
        writer.WriteTerminated(Statements.NonInert());
        writer.Write("END LOOP");
        if (EndLabel != null)
        {
            writer.WriteSql($" {EndLabel}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "LOOP");
        manager.IncreaseIndent();
        foreach (Statement stmt in Statements)
        {
            stmt.FormatSql(writer, manager);
            manager.TerminateStatement(writer, stmt);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, "END LOOP");
        if (EndLabel != null)
        {
            writer.Write(" ");
            EndLabel.FormatSql(writer, manager);
        }
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
