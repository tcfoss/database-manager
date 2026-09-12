using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public abstract record View(ObjectIdentifier Name, Select Body)
    : IDatabaseObject
{
    public ObjectType ObjectType => ObjectType.View;
    public string? RawBodyText { get; init; }

    public virtual bool Equals(View? other)
    {
        if (other == null)
        {
            return false;
        }

        // `RawBodyText` is excluded from the equality contract.
        return Name == other.Name
                && Body == other.Body;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Body);
    }


    public virtual PseudoTable ToPseudoTable()
    {
        return Body.ToPseudoTable(Name.Name, Name, null, PseudoTableType.View);
    }

    public abstract CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null);

    Statement IDatabaseObject.ToCreateStatement(bool includeSchema, DifferFormatManager? manager) => ToCreateStatement(includeSchema, manager);

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
