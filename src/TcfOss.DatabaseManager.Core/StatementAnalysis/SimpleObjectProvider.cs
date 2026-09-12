using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

public class SimpleObjectProvider(Dictionary<ObjectHandle, ObjectType> objects) : IHaveObjects
{
    private readonly Dictionary<ObjectHandle, ObjectType> _objects = objects;

    public ObjectType? GetObjectType(ObjectHandle handle)
    {
        if (_objects.TryGetValue(handle, out ObjectType type))
        {
            return type;
        }
        else
        {
            return null;
        }
    }

    public ObjectType? GetObjectType(SqlValueList<Identifier> name, SchemaIdentifier activeSchema, NameHandling nameHandling)
    {
        ObjectHandle handle = ObjectHandle.Create(name, activeSchema, nameHandling);
        return GetObjectType(handle);
    }
}
