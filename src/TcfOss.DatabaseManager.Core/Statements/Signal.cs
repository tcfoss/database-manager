using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Signal(SignalConditionValue ConditionValue, SqlValueList<SignalInformationItem> InformationItems) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"SIGNAL {ConditionValue}");

        if (InformationItems.Count > 0)
        {
            writer.WriteSql($" SET {InformationItems}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"SIGNAL {ConditionValue}");
        if (InformationItems.Count > 0)
        {
            writer.WriteSql($" SET {InformationItems}");
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
