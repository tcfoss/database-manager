using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record Resignal() : Statement
{
    public SignalConditionValue? ConditionValue { get; init; }
    public SqlValueList<SignalInformationItem> InformationItems { get; init; } = [];

    public override void ToSql(SqlTextWriter writer)
    {
        writer.Write("RESIGNAL");
        if (ConditionValue != null)
        {
            writer.WriteSql($" {ConditionValue}");
        }
        if (InformationItems.Count > 0)
        {
            writer.WriteSql($" SET {InformationItems}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("RESIGNAL");
        if (ConditionValue != null)
        {
            writer.WriteSql($" {ConditionValue}");
        }
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
