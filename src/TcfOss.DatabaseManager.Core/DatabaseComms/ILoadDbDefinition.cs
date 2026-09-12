namespace TcfOss.DatabaseManager.Core.DatabaseComms;

public interface ILoadDbDefinition<TDefinition>
{
    public Task<TDefinition> LoadDefinitionAsync(CancellationToken cancellationToken = default);
}
