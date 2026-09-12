using TcfOss.DatabaseManager.Core.BuiltIn;
using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects.Components;
using TcfOss.DatabaseManager.Core.IO;
using TcfOss.DatabaseManager.Core.StatementAnalysis;
using TcfOss.DatabaseManager.Core.Statements;
using TcfOss.DatabaseManager.Core.Statements.LabelAttributes;

namespace TcfOss.DatabaseManager.Core.DatabaseObjects;

public record StoredFunction(ObjectIdentifier Name, SqlValueList<RoutineParameter> Parameters, DataType ReturnType, Statement Body) : IDatabaseObject
{
    public ObjectType ObjectType => ObjectType.Function;

    public string? RawBodyText { get; init; }

    public virtual bool Equals(StoredFunction? other)
    {
        // `RawBodyText` is excluded from the equality contract.
        if (other == null)
        {
            return false;
        }

        return Name == other.Name
            && Parameters.Equals(other.Parameters)
            && ReturnType == other.ReturnType
            && Body == other.Body;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Parameters, ReturnType, Body);
    }

    public virtual CreateFunction ToCreateStatement(bool includeSchema, DifferFormatManager? manager = null)
    {
        return new CreateFunction(Name.ToObjectName(includeSchema ? 2 : 1), Parameters, ReturnType, Body)
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
