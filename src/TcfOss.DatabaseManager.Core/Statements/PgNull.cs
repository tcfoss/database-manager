using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Statements;

public record PgNull : Statement
{
    public override void ToSql(IO.SqlTextWriter writer)
    {
        writer.Write("NULL");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        yield break;
    }
}
