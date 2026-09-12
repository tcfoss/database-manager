namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public abstract record SqlSource(int SourceId)
{
    public record FileSource(int SourceId, string Path) : SqlSource(SourceId);
}
