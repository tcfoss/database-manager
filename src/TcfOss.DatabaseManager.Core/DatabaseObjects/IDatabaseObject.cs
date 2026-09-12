using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public interface IDatabaseObject
{
    public ObjectType ObjectType { get; }
    public ObjectIdentifier Name { get; }
    public Statement ToCreateStatement(bool includeSchema, DifferFormatManager manager);
    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);
}
