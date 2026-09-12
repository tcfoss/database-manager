using TcfOss.DatabaseManager.Core.DefinitionBuilding;

namespace TcfOss.DatabaseManager.Core.App;

// ReSharper disable UnusedAutoPropertyAccessor.Global : The properties exist to be serialized
public record ParsedDefinition<T>
    where T : class
{
    public required Version Version { get; init; }
    public required DateTime ParsedDate { get; init; }
    public required T Definition { get; init; }
    public required SourceManager Sources { get; init; }
}
