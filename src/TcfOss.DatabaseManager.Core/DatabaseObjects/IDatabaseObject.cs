using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public interface IDatabaseObject<out TStatement> : IHaveObjectIdentifierName
    where TStatement : Statement
{
    public ObjectType ObjectType { get; }
    public TStatement ToCreateStatement(bool includeSchema, DifferFormatManager manager);
    public IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);
}
