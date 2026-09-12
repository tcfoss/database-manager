using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;

namespace TcfOss.DatabaseManager.Core.StatementAnalysis;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IHaveObjects
{
    public ObjectType? GetObjectType(ObjectHandle handle);
    public ObjectType? GetObjectType(SqlValueList<Identifier> name, SchemaIdentifier activeSchema, NameHandling nameHandling);
}
