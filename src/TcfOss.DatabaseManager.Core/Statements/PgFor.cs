using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgFor(SqlValueList<ObjectName> Targets, PgForSource Source, SqlValueList<Statement> Statements) : Statement
{
    public Identifier? EndLabel { get; init; }
    public MetaData LoopMeta { get; init; } = new();

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"FOR {Targets} IN {Source} LOOP ");
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
        manager.WriteBlockPart(writer, "FOR");
        writer.Write(" ");
        writer.FormatDelimited(Targets, manager);
        writer.Write(" IN ");
        Source.FormatSql(writer, manager);
        manager.ExpectingNewLineBeforeStatement = false;
        LoopMeta.FormatPreNonSql(writer, manager);
        writer.Write(writer.EndWithNewLine() ? manager.Indent : " ");
        writer.Write("LOOP");
        manager.ExpectingNewLineBeforeStatement = true;
        manager.IncreaseIndent();
        foreach (Statement statement in Statements)
        {
            statement.FormatSql(writer, manager);
            manager.TerminateStatement(writer, statement);
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
        foreach (ItemRef item in Source.GetReferencedItems(context))
        {
            yield return item;
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
