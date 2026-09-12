using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;

namespace TcfOss.DatabaseManager.Core.Expressions;

public record QualifiedWildcard(ObjectName Qualifier) : Expression
{
    public override void ToSql(SqlTextWriter writer)
    {
        writer.WriteSql($"{Qualifier}.*");
    }

    public override IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        ItemRef? item = context.CreateObjectRef(Qualifier);
        if (item != null)
        {
            yield return item;
        }
    }
}
