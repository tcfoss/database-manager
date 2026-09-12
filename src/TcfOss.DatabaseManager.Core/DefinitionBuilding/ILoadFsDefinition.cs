namespace TcfOss.DatabaseManager.Core.DefinitionBuilding;

public interface ILoadFsDefinition<out TDefinition>
{
    public TDefinition LoadDefinition(bool relaxed);
}
