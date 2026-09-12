namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public interface IReferenceItems
{
    IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context);
}
