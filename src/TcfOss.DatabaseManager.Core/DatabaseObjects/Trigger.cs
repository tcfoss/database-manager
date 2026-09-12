using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public record Trigger(ObjectIdentifier Name, ObjectIdentifier OnTable, Statement Body)
    : IDatabaseObject
{
    public ObjectType ObjectType => ObjectType.Trigger;
    public string? RawBodyText { get; init; }

    public virtual bool Equals(Trigger? other)
    {
        // `RawBodyText` is excluded from the equality contract.
        if (other == null)
        {
            return false;
        }

        return Name == other.Name
                && OnTable == other.OnTable
                && Body == other.Body;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, OnTable, Body);
    }

    public virtual CreateTrigger ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null) => throw new NotSupportedException();

    Statement IDatabaseObject.ToCreateStatement(bool includeSchema, DifferFormatManager? manager) => ToCreateStatement(includeSchema, manager);

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        using (context.Enter(ReferencedItemsContext.CreateTriggerTable))
        {
            ItemRef? item = context.CreateObjectRef(OnTable.ToObjectName(2));
            if (item != null)
            {
                yield return item;
            }
        }

        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
