using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DatabaseObjects;
using TcfOss.DatabaseManager.Core.Statements;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public class DbObjectNode
{
    public required ObjectHandle Handle { get; init; }
    public required ObjectIdentifier Name { get; init; }
    public required ObjectType ObjectType { get; init; }
    public required List<DependencyNode> Dependencies { get; init; }
    public required HashSet<ObjectHandle> HardDependencyHandles { get; init; }
    public required Statement CreateStatement { get; init; }
}
