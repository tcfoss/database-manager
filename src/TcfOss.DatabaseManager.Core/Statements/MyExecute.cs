using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record MyExecute(Identifier StatementName) : Statement
{
    public SqlValueList<Identifier>? Variables { get; init; }

    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"EXECUTE {StatementName}");
        if (Variables.SafeAny())
        {
            writer.WriteSql($" USING {Variables}");
        }
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("EXECUTE ");
        StatementName.FormatSql(writer, manager);
        if (Variables.SafeAny())
        {
            writer.Write(" USING ");
            writer.FormatDelimited(Variables!, manager);
        }
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
