using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Attributes;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DeclareConditionHandler(HandlerAction Action, SqlValueList<ConditionValue> Conditions, Statement Handler) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DECLARE {Action} HANDLER FOR {Conditions} {Handler}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"DECLARE {Action} HANDLER");
        writer.WriteLine();
        manager.IncreaseIndent();
        writer.WriteSqlI("FOR ");
        writer.FormatDelimited(Conditions, manager);
        writer.WriteLine();
        if (Handler is BeginEnd)
        {
            manager.DecreaseIndent();
        }
        Handler.FormatSql(writer, manager);
        manager.DecreaseIndent();
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Handler.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
