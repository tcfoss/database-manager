using TcfOss.DatabaseManager.Core.Common;

namespace TcfOss.DatabaseManager.Core.DefinitionMapping;

public class DependencyNode
{
    public required ObjectHandle Handle { get; init; }
    public required ItemType ItemType { get; init; }
    public required DependencyType DependencyType { get; init; }
}
