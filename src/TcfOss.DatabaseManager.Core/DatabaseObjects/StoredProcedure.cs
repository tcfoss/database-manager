using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public record StoredProcedure(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters, Statement Body) : IDatabaseObject
{
    public ObjectType ObjectType => ObjectType.Procedure;

    public string? RawBodyText { get; init; }

    public virtual bool Equals(StoredProcedure? other)
    {
        // `RawBodyText` is excluded from the equality contract.
        if (other == null)
        {
            return false;
        }

        return Name == other.Name
            && Parameters.Equals(other.Parameters)
            && Body == other.Body;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Parameters, Body);
    }

    public virtual CreateProcedure ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateProcedure(Name.ToObjectName(includeSchema ? 2 : 1), Parameters, Body)
        {
            Meta = new MetaData { RawText = RawBodyText }
        };
    }

    Statement IDatabaseObject.ToCreateStatement(bool includeSchema, DifferFormatManager? manager) => ToCreateStatement(includeSchema, manager);

    public virtual IEnumerable<ItemRef> GetReferencedItems(ReferencedItemsManager context)
    {
        foreach (ItemRef item in Body.GetReferencedItems(context))
        {
            yield return item;
        }
    }
}
