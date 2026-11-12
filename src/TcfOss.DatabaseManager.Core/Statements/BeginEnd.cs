using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record BeginEnd(SqlValueList<Statement> Statements) : Statement
{
    public PgBlockOptions? PgOptions { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        if (PgOptions?.Declarations != null)
        {
            writer.Write("DECLARE ");
            writer.WriteTerminated(PgOptions.Declarations.NonInert());
        }
        writer.Write("BEGIN ");
        writer.WriteTerminated(Statements.NonInert());
        if (PgOptions?.Handlers != null)
        {
            writer.Write("EXCEPTION ");
            foreach (PgExceptionHandler handler in PgOptions.Handlers)
            {
                handler.ToSql(writer);
            }
        }
        writer.Write("END");
        if (PgOptions?.EndLabel != null)
        {
            writer.WriteSql($" {PgOptions.EndLabel}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        if (PgOptions?.Declarations != null)
        {
            manager.WriteBlockPart(writer, "DECLARE");
            manager.IncreaseIndent();
            foreach (Statement declaration in PgOptions.Declarations)
            {
                declaration.FormatSql(writer, manager);
                manager.TerminateStatement(writer, declaration);
            }
            manager.DecreaseIndent();
        }
        manager.WriteBlockPart(writer, "BEGIN");
        manager.IncreaseIndent();
        foreach (Statement stmt in Statements)
        {
            stmt.FormatSql(writer, manager);
            manager.TerminateStatement(writer, stmt);
        }
        manager.DecreaseIndent();
        if (PgOptions?.Handlers != null)
        {
            manager.WriteBlockPart(writer, "EXCEPTION");
            manager.IncreaseIndent();
            foreach (PgExceptionHandler handler in PgOptions.Handlers)
            {
                handler.FormatSql(writer, manager);
            }
            manager.DecreaseIndent();
        }
        manager.WriteBlockPart(writer, "END");
        if (PgOptions?.EndLabel != null)
        {
            writer.Write(" ");
            PgOptions.EndLabel.FormatSql(writer, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        if (PgOptions?.Declarations != null)
        {
            foreach (Statement declaration in PgOptions.Declarations)
            {
                foreach (ItemRef item in declaration.GetReferencedItems(context))
                {
                    yield return item;
                }
            }
        }
        foreach (Statement stmt in Statements)
        {
            foreach (ItemRef item in stmt.GetReferencedItems(context))
            {
                yield return item;
            }
        }
        if (PgOptions?.Handlers != null)
        {
            foreach (PgExceptionHandler handler in PgOptions.Handlers)
            {
                foreach (Statement stmt in handler.Statements)
                {
                    foreach (ItemRef item in stmt.GetReferencedItems(context))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
