using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionBuilding;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public abstract record View(ObjectIdentifier Name, Select Body)
    : IDatabaseObject<CreateView>
{
    public ObjectType ObjectType => ObjectType.View;
    public string? RawBodyText { get; init; }
    public Select? NormalizedBody { get; init; }

    public virtual bool Equals(View? other)
    {
        if (other == null)
        {
            return false;
        }

        Select comparableBody = NormalizedBody ?? Body;
        Select otherComparableBody = other.NormalizedBody ?? other.Body;

        // `RawBodyText` is excluded from the equality contract.
        return Name == other.Name
                && comparableBody == otherComparableBody;
    }

    public override int GetHashCode()
    {
        Select comparableBody = NormalizedBody ?? Body;
        return HashCode.Combine(Name, comparableBody);
    }


    public virtual PseudoTable ToPseudoTable()
    {
        return Body.ToPseudoTable(Name.Name, Name, null, PseudoTableType.View);
    }

    public abstract CreateView ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null);

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
