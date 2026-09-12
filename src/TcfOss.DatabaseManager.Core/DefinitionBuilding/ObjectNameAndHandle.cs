using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public readonly ref struct ObjectNameAndHandle(ObjectIdentifier id, ObjectHandle key)
{
    public ObjectIdentifier Id { get; } = id;
    public ObjectHandle Key { get; } = key;
}
