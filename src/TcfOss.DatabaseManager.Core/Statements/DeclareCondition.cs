using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record DeclareCondition(Identifier Name, ConditionValue Condition) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"DECLARE {Name} CONDITION FOR {Condition}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("DECLARE ");
        Name.FormatSql(writer, manager);
        writer.Write(" CONDITION FOR ");
        Condition.FormatSql(writer, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
