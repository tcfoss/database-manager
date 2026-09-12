using TcfOss.DatabaseManager.Core.Common;
using TcfOss.DatabaseManager.MySql.DatabaseObjects;

namespace TcfOss.DatabaseManager.MySql.DatabaseComms;

public interface IRetrieveDatabaseObjects
{
    Task<List<ObjectIdentifier>> GetTableIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, bool refreshCache = true, CancellationToken cancellationToken = default);
    Task<List<ObjectIdentifier>> GetProcedureIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default);
    Task<List<ObjectIdentifier>> GetFunctionIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default);
    Task<List<ObjectIdentifier>> GetViewIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default);
    Task<List<ObjectIdentifier>> GetEventIdentifiersAsync(SchemaIdentifier schemaId, IEnumerable<string> excludedNames, CancellationToken cancellationToken = default);

    Task<MyTable> GetTableAsync(ObjectIdentifier tableId, CancellationToken cancellationToken = default);
    Task<MyStoredProcedure> GetProcedureAsync(ObjectIdentifier procedureId, CancellationToken cancellationToken = default);
    Task<MyStoredFunction> GetFunctionAsync(ObjectIdentifier functionId, CancellationToken cancellationToken = default);
    Task<Dictionary<ObjectIdentifier, MyTrigger>> GetTriggersOnTableAsync(ObjectIdentifier tableId, CancellationToken cancellationToken = default);
    Task<MyView> GetViewAsync(ObjectIdentifier viewId, CancellationToken cancellationToken = default);
    Task<MyEvent> GetEventAsync(ObjectIdentifier eventId, CancellationToken cancellationToken = default);

    Task<List<string>> GetUnappliedRefactorsAsync(SchemaIdentifier schemaId, IReadOnlyCollection<string> allRefactorIds, CancellationToken cancellationToken = default);
    Task<List<string>> GetUnappliedDeployScriptsAsync(IReadOnlyCollection<SchemaIdentifier> schemaIds, IReadOnlyCollection<string> allDeployScriptIds, CancellationToken cancellationToken = default);
}
