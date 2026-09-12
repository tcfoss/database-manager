using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.DatabaseComms;

public interface IGetUnappliedDeployScripts
{
    public Task<List<DeployScript>> GetAllUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds);
}
