using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public abstract record StoredProcedure<TParam>(
    ObjectIdentifier Name,
    SqlValueList<TParam> Parameters,
    Statement Body)
    : IDatabaseObject<CreateProcedure>
    where TParam : RoutineParameter, IEquatable<TParam>
{
    public ObjectType ObjectType => ObjectType.Procedure;

    public string? RawBodyText { get; init; }

    public virtual bool Equals(StoredProcedure<TParam>? other)
    {
        if (other == null)
        {
            return false;
        }

        // `RawBodyText` is excluded from the equality contract.
        return Name == other.Name
            && Parameters.Equals(other.Parameters)
            && Body == other.Body;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Parameters, Body);
    }

    public abstract CreateProcedure ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null);

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
