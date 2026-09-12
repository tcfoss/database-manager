using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.Core.DefinitionMapping;

namespace TcfOss.DatabaseManager.Core.DatabaseComms;

// Interface also provided for library usage
// ReSharper disable UnusedMemberInSuper.Global
public interface IGetUnappliedRefactors
{
    public Task<IEnumerable<string>> GetUnappliedRefactorIdsAsync(SchemaIdentifier schemaId, IReadOnlyCollection<string> allRefactorIds);
    public Task<List<Refactor>> GetAllUnappliedRefactorsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds);
}
