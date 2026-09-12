using TcfOss.DatabaseManager.Core;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.MsSql.Statements;

/// <summary>
/// T-SQL structured error handling block:
/// <c>BEGIN TRY &lt;stmts&gt; END TRY BEGIN CATCH &lt;stmts&gt; END CATCH</c>.
/// </summary>
public record MsTryCatch(
    SqlValueList<Statement> TryStatements,
    SqlValueList<Statement> CatchStatements) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("BEGIN TRY ");
        writer.WriteTerminated(TryStatements.NonInert());
        writer.Write("END TRY BEGIN CATCH ");
        writer.WriteTerminated(CatchStatements.NonInert());
        writer.Write("END CATCH");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "BEGIN TRY");
        manager.IncreaseIndent();
        foreach (Statement stmt in TryStatements)
        {
            stmt.FormatSql(writer, manager);
            manager.TerminateStatement(writer, stmt);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, "END TRY");
        manager.WriteBlockPart(writer, "BEGIN CATCH");
        manager.IncreaseIndent();
        foreach (Statement stmt in CatchStatements)
        {
            stmt.FormatSql(writer, manager);
            manager.TerminateStatement(writer, stmt);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, "END CATCH");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Statement stmt in TryStatements)
        {
            foreach (ItemRef item in stmt.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        foreach (Statement stmt in CatchStatements)
        {
            foreach (ItemRef item in stmt.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
