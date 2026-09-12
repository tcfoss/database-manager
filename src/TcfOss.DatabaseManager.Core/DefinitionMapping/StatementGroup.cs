using TcfOss.DatabaseManager.Core.Extensions;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public record StatementGroup(SqlValueList<Statement> Substatements) : Statement
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteTerminated(Substatements.NonInert(), ";\n");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (Statement stmt in Substatements)
        {
            foreach (ItemRef item in stmt.GetReferencedItems(context))
            {
                yield return item;
            }
        }
    }
}
