using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgTriggerExecute(ObjectName FunctionName, SqlValueList<PgStatementTriggerArgument> Arguments, bool UseProcedure) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"EXECUTE {(UseProcedure ? "PROCEDURE" : "FUNCTION")} {FunctionName}({Arguments})");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI($"EXECUTE {(UseProcedure ? "PROCEDURE" : "FUNCTION")} ");
        FunctionName.FormatSql(writer, manager);
        writer.WriteSql($"({Arguments})");
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        ItemRef? item = context.CreateObjectRef(FunctionName);
        if (item != null)
        {
            yield return item;
        }
    }
}
