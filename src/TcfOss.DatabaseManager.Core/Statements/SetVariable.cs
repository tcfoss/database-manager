using TcfOss.DatabaseManager.Core.Formatting;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements.Components;

namespace TcfOss.DatabaseManager.Core.Statements;

public record SetVariable(SqlValueList<Assignment> Assignments) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"SET {Assignments.ToSqlDelimited()}");
    }

    public override void FormatSql(SqlTextWriter writer, FormatManager manager)
    {
        Meta.FormatPreNonSql(writer, manager);
        writer.WriteSqlI("SET ");
        writer.FormatDelimited(Assignments, manager);
        Meta.FormatPostNonSql(writer, manager);
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Assignment assignment in Assignments)
        {
            foreach (ItemRef item in assignment.Value.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
