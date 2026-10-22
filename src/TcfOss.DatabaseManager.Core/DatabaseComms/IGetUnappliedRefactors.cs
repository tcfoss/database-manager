using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.DatabaseComms;

public interface IGetUnappliedRefactors
{
    public Task<List<Refactor>> GetAllUnappliedRefactorsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds);
}
