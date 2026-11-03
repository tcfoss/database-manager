using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record BeginAtomic(SqlValueList<Statement> Statements) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("BEGIN ATOMIC");
        foreach (Statement statement in Statements.NonInert())
        {
            writer.Write(" ");
            writer.WriteSql($"{statement};");
        }
        writer.Write(" END");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        manager.WriteBlockPart(writer, "BEGIN ATOMIC");
        manager.IncreaseIndent();
        foreach (Statement statement in Statements)
        {
            statement.FormatSql(writer, manager);
            manager.TerminateStatement(writer, statement);
        }
        manager.DecreaseIndent();
        manager.WriteBlockPart(writer, "END");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Statement statement in Statements)
        {
            foreach (ItemRef item in statement.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
