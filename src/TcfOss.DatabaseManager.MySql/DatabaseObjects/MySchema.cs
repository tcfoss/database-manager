using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.DatabaseManager.MySql.DatabaseObjects;

public record MySchema(SchemaIdentifier Name)
{
    public ValueDict<ObjectIdentifier, MyTable> Tables { get; init; } = [];
    public ValueDict<ObjectIdentifier, MyStoredProcedure> Procedures { get; init; } = [];
    public ValueDict<ObjectIdentifier, MyStoredFunction> Functions { get; init; } = [];
    public ValueDict<ObjectIdentifier, MyTrigger> Triggers { get; init; } = [];
    public ValueDict<ObjectIdentifier, MyView> Views { get; init; } = [];
    public ValueDict<ObjectIdentifier, MyEvent> Events { get; init; } = [];
}
